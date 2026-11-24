"""Validate SQL port settings/rendering without exposing a port or running SQL."""
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
from unittest.mock import patch

root = Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location('configuration', root / 'deployment/configure.py')
configuration = importlib.util.module_from_spec(spec)
spec.loader.exec_module(configuration)
base = {'IMAGE_REF':'ghcr.io/killermaker/indotel:check', 'PUBLIC_DOMAIN':'api.example.com', 'PORTAL_DOMAIN':'portal.example.com'}
with patch.dict(os.environ, base, clear=True):
    defaults = configuration.deployment()
assert defaults['SQL_BIND_ADDRESS'] == '127.0.0.1' and defaults['SQL_PORT'] == '1433'
for changes in [{'SQL_BIND_ADDRESS':'bad-host'}, {'SQL_BIND_ADDRESS':'152.166.124.80/32'},
                {'SQL_PORT':'0'}, {'SQL_PORT':'65536'}, {'SQL_PORT':'8080'}, {'SQL_PORT':'1433\nBAD=1'}]:
    with patch.dict(os.environ, base | changes, clear=True):
        try: configuration.deployment()
        except ValueError: pass
        else: raise AssertionError('Expected rejection: ' + str(list(changes)))
with tempfile.TemporaryDirectory() as directory:
    env_file = Path(directory) / '.env'
    for address, port in [('127.0.0.1','1433'), ('134.209.217.173','1433'), ('127.0.0.1','0')]:
        values = {'APP_IMAGE':base['IMAGE_REF'], 'PUBLIC_DOMAIN':base['PUBLIC_DOMAIN'], 'PORTAL_DOMAIN':base['PORTAL_DOMAIN'],
                  'SQL_BIND_ADDRESS':address, 'SQL_PORT':port}
        env_file.write_text(''.join(k+'='+v+'\n' for k,v in values.items()))
        result = subprocess.run(['docker','compose','--env-file',str(env_file),'-f',str(root/'docker-compose.prod.yml'),'config','--format','json'],
                                env=os.environ | values, capture_output=True, text=True, check=True)
        compose = json.loads(result.stdout)
        ports = compose['services']['sqlserver']['ports']
        assert len(ports) == 1
        assert ports[0]['host_ip'] == address and str(ports[0]['published']) == port and ports[0]['target'] == 1433
        assert compose['services']['sqlserver']['volumes'][0]['source'] == 'sql_data'
print('SQL port checks passed: loopback default, explicit public binding, validation and isolated ephemeral CI port.')
