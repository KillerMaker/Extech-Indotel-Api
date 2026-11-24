"""Local API + SQL/Redis/Kafka, with real Wispro/Siuben TEST integrations."""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import secrets
import subprocess
import sys
import unicodedata
from urllib.parse import urlparse, urlencode
from urllib.request import Request, build_opener, ProxyHandler
from urllib.error import HTTPError, URLError

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
SETTINGS = HERE / 'settings.local.json'
RUNTIME = HERE / '.runtime'

def private_write(path, text):
    path.write_text(text, encoding='utf-8')
    path.chmod(0o600)

def initialize():
    if SETTINGS.exists():
        print('LocalTest/settings.local.json already exists; kept unchanged.')
        return
    values = json.loads((HERE / 'settings.example.json').read_text())
    for name in ['LocalAdminPassword', 'SqlAdminPassword', 'SqlAppPassword', 'RedisPassword', 'JwtSecret']:
        values[name] = 'Local_' + secrets.token_hex(24) + '!9aA'
    private_write(SETTINGS, json.dumps(values, indent=2) + '\n')
    print('Created ignored LocalTest/settings.local.json. Fill WisproApiKey and SiubenPhase2Username/Password before starting.')

def validate(values):
    required = ['WisproClientsUrl', 'WisproContractsUrl', 'WisproApiKey', 'SiubenPhase2Url',
                'SiubenPhase2Username', 'SiubenPhase2Password', 'LocalAdminEmail', 'LocalAdminPassword',
                'SqlAdminPassword', 'SqlAppPassword', 'RedisPassword', 'JwtSecret']
    for name in required:
        if not isinstance(values.get(name), str) or not values[name].strip() or values[name].strip().lower() in ['default', 'placeholder']:
            raise ValueError('Fill ' + name + ' in LocalTest/settings.local.json')
    allowed = {'WisproClientsUrl': ('www.cloud.wispro.co', '/api/v1/clients'),
               'WisproContractsUrl': ('www.cloud.wispro.co', '/api/v1/contracts'),
               'SiubenPhase2Url': ('testindotelapi.siuben.gob.do', '/INDOTELAPI')}
    for name, (host, path) in allowed.items():
        url = urlparse(values[name])
        if (url.scheme != 'https' or url.hostname != host or url.path.rstrip('/') != path
                or url.username or url.password or url.query or url.fragment or url.port not in [None,443]):
            raise ValueError(name + ' must use the confirmed test endpoint from settings.example.json')
    for name in ['ApiPort', 'DatabasePort', 'KafkaPort']:
        if type(values.get(name)) is not int or not 1024 <= values[name] <= 65535:
            raise ValueError(name + ' must be an integer from 1024 to 65535')
    if len({values[n] for n in ['ApiPort', 'DatabasePort', 'KafkaPort']}) != 3:
        raise ValueError('Local ports must be different')
    if type(values.get('ScheduledJobEnabled')) is not bool:
        raise ValueError('ScheduledJobEnabled must be true or false')
    if len(values['JwtSecret'].encode()) < 32:
        raise ValueError('JwtSecret must contain at least 32 UTF-8 bytes')
    if len(values['LocalAdminPassword']) < 12 or len(values['LocalAdminEmail']) > 320 or '@' not in values['LocalAdminEmail']:
        raise ValueError('Use a valid local admin email and password with at least 12 characters')

def render(values):
    validate(values)
    spec = importlib.util.spec_from_file_location('local_deployment', ROOT / 'deployment/configure.py')
    deployment = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(deployment)
    # Reuse production bootstrap/schema logic; only credentials and server are local.
    old = {name: os.environ.get(name) for name in ['MSSQL_SA_PASSWORD','SQL_APP_PASSWORD']}
    try:
        os.environ.update(MSSQL_SA_PASSWORD=values['SqlAdminPassword'], SQL_APP_PASSWORD=values['SqlAppPassword'])
        deployment.sql_configuration()
    finally:
        for name, value in old.items():
            if value is None: os.environ.pop(name, None)
            else: os.environ[name] = value
    RUNTIME.mkdir(exist_ok=True)
    marker = RUNTIME / 'sql-admin.sha256'
    fingerprint = hashlib.sha256(values['SqlAdminPassword'].encode()).hexdigest()
    if marker.exists() and marker.read_text().strip() != fingerprint:
        raise ValueError('SqlAdminPassword changed after bootstrap. Restore the original value or rotate it explicitly in SQL first.')
    sql = deployment.sql_bootstrap(values['SqlAppPassword'])
    email = values['LocalAdminEmail'].replace("'", "''")
    password_hash = hashlib.sha256(values['LocalAdminPassword'].encode()).hexdigest()
    sql += f"\nUSE [Indotel];\nGO\nIF NOT EXISTS (SELECT 1 FROM dbo.AppUser WHERE Email=N'{email}')\nINSERT dbo.AppUser(Email,Password,Name,Phone,RoleId) VALUES(N'{email}','{password_hash}',N'Local Test Admin',N'',1);\nGO\n"
    private_write(RUNTIME / 'sql-init.sql', sql)
    app_password = values['SqlAppPassword'].replace('"', '""')
    environment = {
        'ASPNETCORE_ENVIRONMENT': 'LocalTest', 'DOTNET_ENVIRONMENT': 'LocalTest',
        'ASPNETCORE_HTTP_PORTS': '8080', 'AllowedHosts': '*', 'Swagger__Enabled': 'true',
        'SecretKey': values['JwtSecret'],
        'DatabaseConnectionString': f'Server=sqlserver,1433;Database=Indotel;User Id=indotel_api;Password="{app_password}";Encrypt=True;TrustServerCertificate=True;',
        'WisproClientsUrl': values['WisproClientsUrl'], 'WisproContractsUrl': values['WisproContractsUrl'], 'WisproApiKey': values['WisproApiKey'],
        'Siuben__Phase2__Url': values['SiubenPhase2Url'], 'Siuben__Phase2__Username': values['SiubenPhase2Username'], 'Siuben__Phase2__Password': values['SiubenPhase2Password'],
        # Phase 1 requests fail locally rather than reaching a live phase 1 service.
        'Siuben__Phase1__Url': 'http://127.0.0.1:9', 'Siuben__Phase1__Username': 'disabled', 'Siuben__Phase1__Password': 'disabled',
        'Redis__ConnectionString': 'redis:6379', 'Redis__Password': values['RedisPassword'], 'Redis__InstanceName': 'indotel-local-test:',
        'EmailQueue__BootstrapServers': 'kafka:19092', 'EmailQueue__Topic': 'email.requests.local-test', 'EmailQueue__System': 'indotel-local-test',
        'EmailReciver': 'mailbox@example.test', 'ClientCreationTemplate': 'ClientCreated', 'ClientUpdateRequestTemplate': 'ClientUpdated', 'UserCreationTemplate': 'UserCreated',
        'ScheduledJob__Enabled': str(values['ScheduledJobEnabled']).lower(),
        'Cors__AllowedOrigins__0': 'http://localhost:4301', 'Cors__AllowedOrigins__1': 'http://127.0.0.1:4301',
    }
    runtime = {'services': {
        'api': {'environment': deployment.escape_dollars(environment), 'ports': [f"127.0.0.1:{values['ApiPort']}:8080"]},
        'sqlserver': {'environment': deployment.escape_dollars({'MSSQL_SA_PASSWORD': values['SqlAdminPassword']}), 'ports': [f"127.0.0.1:{values['DatabasePort']}:1433"]},
        'sql-init': {'environment': deployment.escape_dollars({'SQLCMDPASSWORD': values['SqlAdminPassword']})},
        'redis': {'environment': deployment.escape_dollars({'REDIS_PASSWORD': values['RedisPassword']})},
        'kafka': {'ports': [f"127.0.0.1:{values['KafkaPort']}:9092"], 'environment': {'KAFKA_ADVERTISED_LISTENERS': f"INTERNAL://kafka:19092,EXTERNAL://localhost:{values['KafkaPort']}"}},
    }}
    private_write(RUNTIME / 'compose.json', json.dumps(runtime, indent=2))
    private_write(RUNTIME / 'proxy.json', json.dumps({'/api': {'target': f"http://127.0.0.1:{values['ApiPort']}", 'secure': False}}))
    return fingerprint

def compose(*arguments):
    subprocess.run(['docker','compose','--project-name','indotel-local-test','-f',str(HERE / 'compose.yaml'),'-f',str(RUNTIME / 'compose.json'),*arguments], check=True, cwd=ROOT)

def smoke(values, document, locality):
    opener = build_opener(ProxyHandler({}))
    base = f"http://127.0.0.1:{int(values['ApiPort'])}"
    def request(path, data=None, token=None):
        headers = {'Content-Type':'application/json'}
        if token: headers['Authorization'] = 'Bearer ' + token
        with opener.open(Request(base + path, data=json.dumps(data).encode() if data else None, headers=headers), timeout=90) as response:
            return json.load(response)
    request('/health')
    login = request('/api/Authorization/login', {'email':values['LocalAdminEmail'], 'password':values['LocalAdminPassword']})
    token = login.get('token')
    if not token: raise ValueError('Local admin authentication failed')
    print('Local API health and SQL-backed administrator login passed.')
    if document:
        if not re.fullmatch(r'(?:[0-9]{11}|[0-9]{3}-[0-9]{7}-[0-9])', document):
            raise ValueError('Use an 11-digit test document, with optional 000-0000000-0 mask')
        configured = json.loads((ROOT / 'appsettings.json').read_text())['Siuben']['Phase2']['Localities']
        if locality not in configured: raise ValueError('Choose a configured phase 2 locality')
        response = request('/api/Client/check?' + urlencode({'documentNumber':document, 'locality':locality}), token=token)
        if response.get('siubenPhase') != 2: raise ValueError('Expected Siuben phase 2 response')
        print('Wispro and Siuben phase 2 lookup completed. Exists in Wispro:', response.get('existsInWispro'), '| Exists in Siuben:', response.get('existsInSiuben'), '| Eligible:', response.get('isEligible'))

def probe(values):
    validate(values)
    # Only authenticate/read. The synthetic zero document avoids fetching client records.
    def request(label, url, headers=None, data=None, allow_not_found=False, allow_invalid_synthetic=False):
        headers = {'Content-Type':'application/json', **(headers or {})}
        try:
            with build_opener().open(Request(url, headers=headers, data=json.dumps(data).encode() if data else None), timeout=40) as response:
                result = json.load(response)
                print(label + ': HTTP ' + str(response.status))
                return result
        except HTTPError as error:
            if allow_not_found and error.code == 404:
                print(label + ': HTTP 404 (synthetic test document not found, expected)')
                return None
            if allow_invalid_synthetic and error.code == 403:
                try:
                    detail = json.load(error).get('extraInformation', '')
                    normalized = unicodedata.normalize('NFKD', detail).encode('ascii', 'ignore').decode().lower().strip()
                    if normalized == 'cedula invalida':
                        print(label + ': HTTP 403 (synthetic all-zero document rejected as invalid, expected)')
                        return None
                except (ValueError, AttributeError, TypeError): pass
            hint = 'The endpoint/path was not found.' if error.code == 404 else 'Verify test credentials and service access.'
            raise ValueError(label + ': HTTP ' + str(error.code) + '. ' + hint) from None
        except (URLError, json.JSONDecodeError):
            raise ValueError(label + ': connection or JSON response failed. Check the test service/network.') from None
    failures = []
    try:
        login = request('Siuben phase 2 login', values['SiubenPhase2Url'].rstrip('/') + '/api/auth/login',
                        data={'name':values['SiubenPhase2Username'], 'password':values['SiubenPhase2Password']})
        token = login.get('token')
        if not token: raise ValueError('Siuben phase 2 did not return a token')
        request('Siuben phase 2 lookup', values['SiubenPhase2Url'].rstrip('/') + '/api/data_fase2/get/00000000000',
                headers={'Authorization':'Bearer ' + token}, allow_not_found=True, allow_invalid_synthetic=True)
    except ValueError as error: failures.append(str(error))
    for name, query in [('WisproClientsUrl','national_identification_number_eq'), ('WisproContractsUrl','client_national_identification_number_eq')]:
        try:
            result = request(name, values[name] + '?' + urlencode({query:'00000000000'}), headers={'Authorization':values['WisproApiKey']})
            if result.get('status') != 200: raise ValueError(name + ': API status was not 200')
        except ValueError as error: failures.append(str(error))
    if failures: raise ValueError('\n'.join(failures))
    print('External authentication/read checks completed; no clients or beneficiary statuses were changed.')

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['init','configure','up','down','status','logs','smoke','probe'])
    parser.add_argument('--document', help='Optional test cedula for a read-only lookup in both test services (smoke only)')
    parser.add_argument('--locality', default='El Naranjo')
    args = parser.parse_args()
    if args.command == 'init': initialize(); return
    if args.command == 'probe':
        if not SETTINGS.exists(): raise ValueError('Initialize the local settings first')
        probe(json.loads(SETTINGS.read_text(encoding='utf-8-sig')))
        return
    if args.command == 'smoke':
        if not SETTINGS.exists(): raise ValueError('Initialize the local settings first')
        smoke(json.loads(SETTINGS.read_text(encoding='utf-8-sig')), args.document, args.locality)
        return
    if args.command in ['configure','up']:
        if not SETTINGS.exists():
            raise ValueError('Run python LocalTest/run.py init first')
        values = json.loads(SETTINGS.read_text(encoding='utf-8-sig'))
        fingerprint = render(values)
        if args.command == 'configure': print('Local configuration validated and rendered. No external calls made.'); return
        compose('config','--quiet')
        compose('up','-d','--no-recreate','--wait','--wait-timeout','300','sqlserver','redis','kafka')
        compose('run','--rm','--no-deps','-T','sql-init')
        private_write(RUNTIME / 'sql-admin.sha256', fingerprint + '\n')
        compose('run','--rm','--no-deps','-T','kafka-init')
        compose('up','-d','--no-deps','--build','--wait','--wait-timeout','180','api')
        print(f"API: http://localhost:{values['ApiPort']}/swagger | Login email: {values['LocalAdminEmail']}")
        print('Local login password is LocalAdminPassword in LocalTest/settings.local.json. No SMTP sender is running.')
    else:
        if not (RUNTIME / 'compose.json').exists(): raise ValueError('Configure the local environment first')
        if args.command == 'down': compose('down')
        elif args.command == 'status': compose('ps','-a')
        else: compose('logs','--tail','100','api')

if __name__ == '__main__':
    try: main()
    except HTTPError as error:
        print(f'Local API request failed with HTTP {error.code}. Check local logs and test credentials.', file=sys.stderr)
        sys.exit(1)
    except URLError:
        print('Local API is not reachable. Start the local environment first.', file=sys.stderr)
        sys.exit(1)
    except (ValueError, OSError, subprocess.CalledProcessError):
        # Avoid printing provider/process errors which may include credentials.
        # Configuration errors contain only fixed field names and instructions.
        error = sys.exc_info()[1]
        print(str(error) if isinstance(error, ValueError) and not isinstance(error, json.JSONDecodeError) else 'Local environment command failed. Check Docker Desktop (Linux containers) and the local settings.', file=sys.stderr)
        sys.exit(1)
