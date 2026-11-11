# Simulación integrada local: Exatech-Indotel-Simulation

`Exatech-Indotel-Simulation.csproj` es un proyecto web .NET 10 separado, incluido en `Exatech-Indotel-API.sln`. Los dos archivos `.csproj` están juntos en la raíz, sin carpetas virtuales en la solución. El código de simulación sigue agrupado bajo `Simulation/`, con salidas independientes en `bin/Simulation/` y `obj/Simulation/`. Referencia el API y reutiliza sus controladores, servicios y validadores. El proyecto principal excluye `Simulation/**` de compilación y contenido; su `Program.cs` no contiene condiciones ni endpoints de simulación.

El frontend Angular normal (sin el interceptor del demo) llama `/api` a través del proxy de desarrollo. El API .NET real ejecuta autenticación JWT, validadores, controladores, servicios y proxies HTTP de producción. Siuben fase 1/2 y Wispro responden desde un servidor HTTP simulado. Los repositorios de clientes/usuarios y el caché se sustituyen por implementaciones temporales en memoria. No se prueba SQL Server, migraciones ni Redis en este modo.

## Arrancar

Requisitos: PowerShell 7, .NET SDK 10 y Node/npm. Ejecutar `npm install` en el frontend la primera vez. El script compila y restaura el proyecto de simulación. Python 3 se utiliza únicamente para la verificación opcional `verify.py`; no se necesita para ejecutar los servicios.

Desde la raíz del API:

```powershell
./Simulation/Start-Simulation.ps1 -FrontendPath "C:/ruta/Exatech-Indotel-Portal"
```

- Frontend: http://127.0.0.1:4300
- API: http://127.0.0.1:5188
- Siuben y Wispro simulados en C#, dentro del mismo proceso .NET: http://127.0.0.1:5199
- Usuario: `sim@example.com`
- Contraseña: `Simulation123!`
- Usuario Seller para probar accesos denegados: `seller@example.com`, misma contraseña.

Solo `Admin` puede habilitar/deshabilitar manualmente, crear usuarios o ejecutar la sincronización manual. Las llamadas anónimas reciben 401; Seller recibe 403.

Para iniciar solo el host .NET (API y mocks):

```powershell
dotnet run --project Exatech-Indotel-Simulation.csproj
```

El frontend puede arrancarse por separado con `npm run simulation` en su repositorio. El script de arranque anterior inicia ambos.

Todo escucha solo en loopback. El host usa exclusivamente `simulation.settings.json` y las credenciales/URLs ficticias de `SimulationSetup`; no carga la configuración, variables ni secretos del API real. Las localidades se editan en `Simulation/simulation.settings.json`. No registra SQL, Redis ni el productor Kafka. Los correos se registran sin enviarse. El transporte HTTP rechaza destinos distintos al simulador, desactiva proxies y no sigue redirecciones. No se omite autenticación JWT. Swagger se omite en simulación por la incompatibilidad existente de Swashbuckle 6.4/OpenAPI 2 con .NET 10.

## Casos preparados

| Cédula | Localidad | Estado Wispro | Caso |
|---|---|---|---|
| 00100118793 | Bombita | enabled | Sincronizar → habilitar |
| 66666666666 | Bombita | disabled | Par: ya sincronizado |
| 22222222224 | Bombita | enabled | Par: ya sincronizado |
| 55555555555 | Bombita | disabled | Impar: pendiente de deshabilitar, motivo editable |
| 77777777777 | Bombita | alerted | Sincronización rechazada |
| 99999999999 | Bombita | Sin contrato | Consulta local visible; sincronización bloqueada |
| 88888888888 | Bombita | Sin cliente inicialmente | Consulta elegible, crear cliente |
| 11111111111 | Bombita | No existe | No encontrado |
| 22222222222 | Bombita | No existe | No elegible en Siuben |

Para fase 1, consultar una cédula nueva con localidad Salinas. Se usan las localidades de `Simulation/simulation.settings.json`; los ejemplos anteriores presuponen que Bombita/Salinas están configuradas.

Los clientes creados no reciben un contrato automáticamente. Agregar/cambiar un contrato simulado:

```powershell
Invoke-RestMethod http://127.0.0.1:5199/control/contract -Method Put -ContentType application/json -Body '{"documentNumber":"88888888888","state":"enabled","contractNumber":2000}'
```

Cambiar `state` a `disabled` o `alerted` permite probar las restricciones. `GET http://127.0.0.1:5199/control/state` muestra los datos simulados y el registro de llamadas HTTP (incluidos los logins de Siuben). El token Siuben simulado expira en una hora y el API lo reutiliza desde su caché temporal.

## Job y correos

La programación diaria se desactiva en simulación para controlar las pruebas. Ejecutar exactamente la lógica del job con `POST /simulation/synchronize` y un Bearer token obtenido de `POST /api/authorization/login`. `GET /simulation/emails` devuelve los tipos de correos suprimidos, con autenticación.

## Verificar, parar y reiniciar

En un entorno recién arrancado:

```powershell
python Simulation/verify.py
./Simulation/Stop-Simulation.ps1
```

La verificación usa el proxy Angular y realiza 48 comprobaciones HTTP: login, permisos Admin/Seller y rechazo sin efectos externos, validación, fases 1/2, consulta local, creación, habilitar/deshabilitar, restricciones Wispro, job, correos suprimidos y un único login Siuben por fase. Modifica solo datos simulados. Para repetirla, parar y arrancar antes.

El script de parada termina únicamente procesos registrados con el mismo PID y hora de inicio. Reiniciar restaura las semillas y descarta clientes, contratos, estados, caché y correos temporales. Logs y PIDs se guardan en `.simulation/` (ignorado por Git).

Las semillas con contrato `enabled` o `disabled` usan el último dígito de la cédula:
par = estados coincidentes; impar = estados opuestos. El simulador HTTP de Siuben
y el repositorio local parten del mismo estado. Esta regla se aplica únicamente
al cargar datos iniciales, no a producción ni a las acciones posteriores. Los
clientes sin contrato o con estado `alerted` siguen bloqueados. Reiniciar restaura
las semillas; sincronizar o ejecutar el job puede cambiar su estado después.
