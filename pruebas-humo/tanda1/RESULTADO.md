# Pruebas de humo en Dev, tanda 1 (2026-10-20)

Dev `https://org36e60d9d.crm.dynamics.com/`, paquete `sanic_carp_pkg_plugins` 1.0.0 real. Todo como administrador (usuario de aplicación, "persona", no SYSTEM); CallerId = Randy Kauffman DEV solo como cabecera. Datos `HUMO-`. Filas creadas: `estado.json` (no se borró nada). Evidencia: `evidencia/*.json` (sin tokens ni datos personales). Scripts: `h*.py`.

Estado final de Dev: v91 Vigente, v90 Histórica, v92 y v94 Borradores desechables, ~12 arreglos, 1 Terminado.

| Prueba | Resultado | Evidencia | Notas |
|---|---|---|---|
| H-14 | CONFIRMADO (a: por inferencia) | 10_h14_h15.json | (b,d) `ZZV-NOMBRE`, `conv-000001`, `CONV-12345`, `CONV-000001 ` rechazados con CARP-CNV-001 y sin fila; `CONV-777777` se guarda tal cual. (a) el alta sin nombre sale `CONV-001154`: con el código de producción no se ve el Target; solo se infiere que no trae un valor de formato inválido. (c) Update de persona no dispara CNV-001 por nombre. |
| H-15 | CONFIRMADO | 10_h14_h15.json | Barrido de las 21 columnas con valor en Abierta y Finalizada: todas detectadas. Nulas reenviadas como null no son cambio. Un caso marcado XX (`sinrespuestadesde=null` en Finalizada) es error del script: Finalizar ya la deja null. |
| H-29 | PARCIAL | 11_h29_*.json | Confirmado: InitiatingUserId = CallerId (tomar asigna a Randy; unir `finalizadoporid`=Randy); salidas Guid/Integer/Boolean/String correctas; trace de la API: depth 1, mode 0, primaryentity `none`, MessageName = uniquename; pasos anidados depth 2. No observable con el código: Stage y UserId (el trace log no los guarda y ningún código los usa). |
| H-12 (a) | CONFIRMADO | 30_h10_h21_h12.json | Reemplazo por PATCH sobre Vigente: CARP-VER-003 y el archivo queda igual. |
| H-12 (b) | NO CORRIDA | | usuario no admin: tanda 2. Como admin, subida y reemplazo sobre Borrador pasan (2 pasadas, traza de S-27 x3 por versión). |
| H-12 (c) | CONFIRMADO | idem | `DELETE .../sanic_archivo` sobre Vigente: S-27 lo ve, CARP-VER-003, archivo conservado. No hace falta otro registro. |
| H-23 | CONFIRMADO | 20_h12_h23.json, 21 | Create con `statecode=1` es aceptado pero nace Activo/Borrador (versión y mapeo); con `statecode=1,statuscode=2` la plataforma da 400 ("2 is not a valid status code for state code ...Active"). Una persona no crea filas inactivas. |
| H-10 | CONFIRMADO | 30_h10_h21_h12.json | v90 y v91 publicadas (0,55 a 0,77 s, archivo de 32 KB); huella = SHA-256 local; aprobador = actor; v90 pasa a Histórica; una sola Vigente. Rechazos VER-005, VER-007, VER-009 (lista las hojas reales). |
| H-21 (a) | CONFIRMADO | idem | S-26 a profundidad 2 sale correcto al pasar v90 a Histórica (SYSTEM anidado). |
| H-21 (b) | CONFIRMADO (conductual) | idem | Reenvío de nulos y de iguales en Versión y Mapeo pasa; cambios reales se detectan. |
| H-21 (c) | PARCIAL | idem | Solo se tiene la plantilla de 32 KB (no la real de 5 MB): publicar tarda < 1 s. El caso de 5 MB sigue sin medirse. |
| H-21 (d) | NO CORRIDA | | borraría un Borrador (instrucción de la tanda). |
| H-21 (e) | CONFIRMADO con matiz | idem | `messageblock` (ITracingService) sin nombres de hoja. El campo `exceptiondetails` del trace log, que escribe la plataforma, sí repite el texto del error, que incluye nombres de hoja (estructura de plantilla, no datos de clientes). |
| H-16 (a) | MEDIDO | 40_h16_h17_h3.json | Create con `statecode=1` sin statuscode: el step NO rechaza y la fila nace Borrador. El Target de PreOperation trae el 0 de la plataforma. `CARP-ARR-008` no se dispara por esa vía (falla abierto al mismo estado que la plataforma; inocuo). |
| H-16 (b) | CONFIRMADO con matiz | idem | 19 de 20 columnas nulas reenviadas como null pasan. `sanic_fechacongelamiento: null` en un Borrador sin congelar da CARP-ARR-017 porque el diseño lo toma como gesto Desbloquear (ReglasDeArreglo.cs ~392 y 523): no es un fallo, pero una fila completa reenviada con ese null se rechaza. Cambios reales se detectan. |
| H-16 (c) | NO CORRIDA | | usuario no admin: tanda 2. |
| H-17 | CONFIRMADO: AwayFromZero | idem | 5.565 -> 5.57 y 5.575 -> 5.58 (saldo inicial, remanente, total y cuota). |
| H-3 | CONFIRMADO | idem | 60 cuotas: alta completa en 4,84 s (S-20 4,4 s) frente al límite de 120 s. |
| H-19 | CONFIRMADO | 50_h19_h18_h2_h4.json | Pagar la última cuota como persona deja el arreglo Terminado (1/2, fechatermino) sin CARP-ARR-010. S-04 en cuotas (depth 2) tarda mediana 31 ms contra 62 a 140 ms a depth 1: es el atajo de SYSTEM (inferencia). |
| H-18 | CONFIRMADO | idem | Nulos reenviados no son cambio; `sanic_monto` y `statecode` dan CUO-001; fecha sola da CUO-003; vaciar solo la fecha de una cuota con pago da CUO-003. |
| H-2 | CONFIRMADO | idem | Segundo formalizar secuencial: CARP-ARR-020. En carrera real: una 204 y la otra 412 por la clave K-02. |
| H-4 (admin) | CONFIRMADO | idem | generarformato directo: congela, provisional 1, bitácora con generadoporid = actor, `advertencias` vacío. Archivo: `evidencia/H-11_F-NIC-0000052-HUMO.xlsx`. Usuario no admin: tanda 2. |
| H-31 | PARCIAL | 60_h31.json | 5 rondas de dos tomar simultáneos: siempre un solo oficial, el perdedor recibe CARP-TOM-900; la traza muestra la falla `0x80060882`. K-01 por Web API: 412. K-01 dentro de un plugin y el 0x80040265 anidado: no corridos (exigen correos). |

## Hallazgos que piden decisión o cambio

1. El diseño y el diccionario (línea 613) dicen 27 filas en MAP_V07; el mockup (líneas 262 a 271) y `tools/Medicion` tienen **28** (clienteNombre y fechaFormalizacion van dos veces). Se cargaron 28 y publican bien. Corregir el texto.
2. `ArregloCrearStep` (CARP-ARR-008 por `statecode=1`): rama inalcanzable con statecode solo, porque la plataforma lo deja en 0 antes del step. Hay que ajustar el texto de H-16 en 2.5 y 8.1.
3. Reenviar `sanic_fechacongelamiento: null` en un Borrador sin congelar da CARP-ARR-017 (ReglasDeArreglo.cs ~392 y 523). Conviene documentarlo para el formulario y el web resource.
4. H-21 (c) sigue abierto: falta la plantilla de 5 MB.

No quedó ninguna prueba de prioridad A refutada.
