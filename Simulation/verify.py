"""Integration checks through Angular's proxy -> real API -> HTTP mocks + simulated repositories."""
import json
import time
from urllib.request import Request, urlopen
from urllib.error import HTTPError, URLError

BASE = 'http://127.0.0.1:4300'
token = None
checks = 0

def call(path, method='GET', body=None, expected=200, base=BASE):
    global checks
    headers = {'Content-Type': 'application/json'}
    if token: headers['Authorization'] = 'Bearer ' + token
    request = Request(base+path, data=json.dumps(body).encode() if body is not None else None, headers=headers, method=method)
    try:
        with urlopen(request, timeout=15) as response: status, raw = response.status, response.read()
    except HTTPError as error: status, raw = error.code, error.read()
    assert status == expected, (method, path, status, raw.decode(errors='replace'))
    checks += 1
    return json.loads(raw) if raw else None

for attempt in range(45):
    try:
        call('/simulation/health')
        break
    except (URLError, AssertionError):
        if attempt == 44: raise
        time.sleep(1)
call('/api/client?documentNumber=00100118793', expected=401)
mutation_routes = ['/api/siuben/synchronize']
for route in mutation_routes:
    call(route+'?locality=Bombita', 'PUT', {'cedula':'00100118793'}, expected=401)
# A signed Seller token must be rejected even for direct HTTP calls.
token = call('/api/authorization/login', 'POST', {'email':'seller@example.com','password':'Simulation123!'})['token']
state_before = call('/control/state', base='http://127.0.0.1:5199')
for route in mutation_routes:
    call(route+'?locality=Bombita', 'PUT', {'cedula':'00100118793'}, expected=403)
call('/simulation/synchronize', 'POST', expected=403)
call('/api/user/create', 'POST', {}, expected=403)
call('/api/siuben/localities')
state_after = call('/control/state', base='http://127.0.0.1:5199')
assert state_before == state_after, 'unauthorized mutations must have no external side effects'
token = call('/api/authorization/login', 'POST', {'email':'sim@example.com','password':'Simulation123!'})['token']
localities = call('/api/siuben/localities')
assert any(x['name']=='Bombita' and x['phase']==2 for x in localities)
assert call('/api/client?documentNumber=001-0011879-3')['client']['wisproId']=='sim-00100118793'
for document in ['00100118793', '66666666666', '22222222224', '55555555555']:
    details = call('/api/client?documentNumber='+document)
    assert details['alreadySynchronized'] == (int(document[-1]) % 2 == 0), 'seed parity determines initial synchronization'
    assert details['canSynchronize'] == (int(document[-1]) % 2 != 0)

call('/api/client?documentNumber=11111111111', expected=404)
call('/api/client?documentNumber=bad', expected=400)
missing_contract = call('/api/client?documentNumber=99999999999')
assert missing_contract['wisproContract'] is None and not missing_contract['canSynchronize']
call('/api/siuben/synchronize', 'PUT', {'cedula':'99999999999'}, expected=409)

assert call('/api/client/check?documentNumber=88888888888&locality=Bombita')['isEligible']
assert call('/api/client/check?documentNumber=88888888888&locality=Salinas')['pobertyLevel']==2
call('/api/siuben/synchronize', 'PUT', {'cedula':'00100118793'})
assert call('/api/client?documentNumber=00100118793')['client']['siubenActivated']
before_repeat = call('/control/state', base='http://127.0.0.1:5199')
repeat = call('/api/siuben/synchronize', 'PUT', {'cedula':'00100118793','motivoCancelacion':'Cancelación'}, expected=409)
assert repeat['clientDetails']['alreadySynchronized'] and not repeat['clientDetails']['canSynchronize']
after_repeat = call('/control/state', base='http://127.0.0.1:5199')
assert sum(c['path'].endswith('HabilitarBeneficiado') for c in before_repeat['calls']) == sum(c['path'].endswith('HabilitarBeneficiado') for c in after_repeat['calls'])

call('/api/siuben/synchronize', 'PUT', {'cedula':'55555555555','motivoCancelacion':'Otro','otroMotivoCancelacion':'Prueba integrada'})
assert call('/api/client?documentNumber=66666666666')['client']['siubenDisabled']
assert call('/api/client?documentNumber=66666666666')['alreadySynchronized']
call('/api/siuben/synchronize', 'PUT', {'cedula':'66666666666'}, expected=409)

call('/api/siuben/synchronize', 'PUT', {'cedula':'77777777777'}, expected=409)
new = dict(name='Nuevo simulado', email='nuevo@example.com', nationalIdentificationNumber='88888888888', locality='Bombita', phone='8095551234', phoneMobile='', street='Calle simulada', number='20', city='Barahona', state='Barahona', pobertyLevel=0)
call('/api/client/create', 'POST', new)
assert call('/api/client?documentNumber=88888888888')['client']['name']=='Nuevo simulado'
assert not call('/api/client?documentNumber=88888888888')['canSynchronize']
call('/api/siuben/synchronize', 'PUT', {'cedula':'88888888888'}, expected=409)
for old_route in ['/api/siuben/enable', '/api/siuben/enable-stored', '/api/siuben/disable']:
    call(old_route, 'PUT', {'cedula':'00100118793'}, expected=404)

call('/control/contract', 'PUT', {'documentNumber':'88888888888','state':'enabled','contractNumber':2000}, base='http://127.0.0.1:5199')
call('/simulation/synchronize', 'POST')
assert call('/api/client?documentNumber=88888888888')['client']['siubenActivated']
call('/control/contract', 'PUT', {'documentNumber':'88888888888','state':'disabled'}, base='http://127.0.0.1:5199')
call('/simulation/synchronize', 'POST')
assert call('/api/client?documentNumber=88888888888')['client']['siubenDisabled']
assert 'ClientCreation' in call('/simulation/emails')
state=call('/control/state',base='http://127.0.0.1:5199')
assert not state['beneficiaries']['88888888888']
assert sum(c['path']=='/api/auth/login' for c in state['calls'])==2, 'one cached Siuben login per phase'
print(f'Passed {checks} HTTP integration checks, plus state/payload assertions. Frontend -> API -> mocks verified.')
