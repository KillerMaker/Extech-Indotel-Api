"""Validate local configuration without credentials, external calls or running containers."""
import copy
import importlib.util
import json
from pathlib import Path
import subprocess
import tempfile

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('local_run', HERE / 'run.py')
local = importlib.util.module_from_spec(spec)
spec.loader.exec_module(local)
values = json.loads((HERE / 'settings.example.json').read_text())
values.update(WisproApiKey='test-key', SiubenPhase2Username='test-user', SiubenPhase2Password='test-password',
              LocalAdminPassword='LocalPortal123!', SqlAdminPassword='LocalSqlAdmin123!', SqlAppPassword="LocalSqlApp';$123!",
              RedisPassword='LocalRedis123!', JwtSecret='local-test-signing-key-at-least-32-bytes')
local.validate(values)
for name, bad in [('WisproApiKey',''), ('SiubenPhase2Password','placeholder'), ('SiubenPhase2Url','https://servicios.siuben.gob.do'),
                  ('WisproClientsUrl','https://evil.example/api/v1/clients'), ('ApiPort',80), ('DatabasePort',5288),
                  ('KafkaPort',True), ('ScheduledJobEnabled','false'), ('JwtSecret','short')]:
    sample = copy.deepcopy(values); sample[name] = bad
    try: local.validate(sample)
    except ValueError: pass
    else: raise AssertionError('Expected configuration rejection for ' + name)

with tempfile.TemporaryDirectory() as directory:
    local.RUNTIME = Path(directory)
    fingerprint = local.render(values)
    runtime = json.loads((local.RUNTIME / 'compose.json').read_text())
    api = runtime['services']['api']['environment']
    assert api['ScheduledJob__Enabled'] == 'false'
    assert api['Siuben__Phase1__Url'] == 'http://127.0.0.1:9'
    assert api['Siuben__Phase2__Url'] == 'https://testindotelapi.siuben.gob.do/INDOTELAPI'
    assert api['EmailQueue__BootstrapServers'] == 'kafka:19092'
    assert 'Server=sqlserver,1433;' in api['DatabaseConnectionString']
    assert '$$' in api['DatabaseConnectionString']
    sql = (local.RUNTIME / 'sql-init.sql').read_text()
    assert "LocalSqlApp'';$123!" in sql
    assert 'LocalPortal123!' not in sql
    assert "'ClientCreated'" in sql and "N'Local Test Admin'" in sql
    result = subprocess.run(['docker','compose','--project-name','indotel-local-test','-f',str(HERE / 'compose.yaml'),'-f',str(local.RUNTIME / 'compose.json'),'config','--format','json'], capture_output=True, text=True, check=True)
    compose = json.loads(result.stdout)
    assert compose['name'] == 'indotel-local-test'
    for name, port in [('api','5288'),('sqlserver','11433'),('kafka','12992')]:
        binding = compose['services'][name]['ports'][0]
        assert binding['host_ip'] == '127.0.0.1' and str(binding['published']) == port
    assert 'ports' not in compose['services']['redis']
    assert values['SqlAppPassword'] in compose['services']['api']['environment']['DatabaseConnectionString'].replace('$$', '$')
    assert 'Server=sqlserver,1433;' in compose['services']['api']['environment']['DatabaseConnectionString']
    assert not any('smtp' in name for name in compose['services'])
    (local.RUNTIME / 'sql-admin.sha256').write_text(fingerprint)
    sample = copy.deepcopy(values); sample['SqlAdminPassword'] = 'ChangedSqlAdmin123!'
    try: local.render(sample)
    except ValueError: pass
    else: raise AssertionError('Changing the persisted SA password must be rejected')
print('Local test configuration checks passed: validation, SQL seed, secret escaping, loopback ports, service isolation and password retention.')
