# Prueba de roles con un usuario no administrador (2026-10-09)

Entorno de desarrollo (`org36e60d9d`). Usuario: el de aplicación del service principal de correo (`PP_MAIL_*`), movido a la BU `Arreglos de Pago`. Scripts: `prueba_roles.py` (primera vuelta) y `prueba_roles_2.py` (segunda vuelta, la que vale); resultados en `resultado.json` y `resultado_2.json`. Cierra las dudas D-01, D-02, D-03 y D-07 de `diseno/04-matriz-privilegios.md` §5. D-04 (purga de queue items) queda para cuando exista la queue del área.

## Configuración que queda en Dev

- `SR - CARP - Oficial` (copia de la BU) asignado al team del sistema `Arreglos de Pago`.
- `SR - CARP - Jefe` (copia de la BU) asignado al team `Arreglos de Pago - Jefatura`.
- El usuario de prueba queda en la BU `Arreglos de Pago`, fuera de Jefatura y **deshabilitado**. Los datos de prueba se borraron.

## Resultados

| Caso | Resultado | Qué significa |
|---|---|---|
| C1. Crear Versión con el team como dueño, con Create y **sin Assign** | 201 | **D-02 refutada: Assign NO hace falta** para crear con el team como dueño |
| C2. Crear Versión propia (control) | 201 | — |
| A2 (1.ª vuelta). Crear Cliente con el team como dueño cuando el team no tenía roles | 403 `Read Privilege Check For Owner failed … Principal team … privilegeCount=0` | **La plataforma exige que el team DUEÑO tenga Read sobre la tabla** |
| B8a. Mapeo con el team del sistema como dueño | 403, mismo mensaje: el team tiene el rol Oficial, que no lee Mapeo | Con el diseño actual, el team del sistema **no puede ser dueño de un Mapeo** |
| B8b. Mapeo con el team Jefatura como dueño | 201 | Funciona: Jefatura tiene el rol Jefe, que lee Mapeo |
| B9. El Jefe lee el Mapeo | 200 | D-01 cerrada (el Read agregado funciona) |
| B1. Oficial crea Cliente con el team como dueño | 201 | — |
| B2. Oficial lee una Conversación del team | 200 | — |
| B3. Oficial crea Arreglo con lookup a usuario, cliente y conversación | 403 `is missing prvAppendToUser privilege … on OTC=8 for entity 'systemuser'` | **D-07 confirmada: hace falta `prvAppendToUser`** para cualquier lookup a usuario |
| B4. Oficial crea un borrador de Email con Regarding a la conversación | 201 | — |
| B5. Oficial adjunta un archivo al borrador (`activitymimeattachment`) | 201 | **D-03 cerrada**: el adjunto no pide privilegios propios |
| B6. Oficial lee el adjunto | 200 | — |
| B7. Jefe crea Versión con el team como dueño | 201 | — |
| B10. Oficial intenta borrar un Cliente | 403 | Sin Delete humano, como pide el diseño |

## Consecuencias para el diseño

1. **Quitar Assign** del Oficial (Cliente, Arreglo) y del Jefe (Versión, Mapeo). Solo estaba para fijar el team como dueño, y no hace falta. Sin Assign nadie puede reasignar un registro fuera del team, que es más seguro que depender solo del plugin pre-update.
2. **Agregar `prvAppendToUser`** al Oficial (profundidad BU).
3. **Dueño de Mapeo**: el team del sistema no puede serlo mientras el Oficial no lea Mapeo. Decisión del usuario pendiente: o el Oficial lee Mapeo, o Mapeo es del team Jefatura.
4. Regla general para el plugin de P-11: el team que se pone como dueño tiene que tener Read sobre la tabla; si no, el alta falla con 403.

## Tropiezos de la prueba

- La primera vuelta no creó la conversación de prueba porque el nombre tenía 22 caracteres y la primaria admite 20 (error del script, no de la plataforma).
- Crear un rol con el mismo nombre de uno recién borrado falló una vez y funcionó al reintentar: la baja del rol en las BU hijas no es instantánea.

## Tercera vuelta, con los cambios aplicados (2026-10-09)

Decisión del usuario: opción A (el Oficial lee Mapeo; todo es del team del sistema). Se quitó Assign del Oficial (Cliente, Arreglo) y del Jefe (Versión, Mapeo), se agregó `prvAppendToUser` (BU) y Read de Mapeo al Oficial, y se quitó al Jefe el Read de Mapeo (le llega por el Oficial). `prueba_roles_2.py` otra vez, resultados en `resultado_3.json`: **B3 (lookup a usuario) y B8a (Mapeo con el team del sistema como dueño) pasan**; el resto igual. B8b (Mapeo con el team Jefatura como dueño) ahora da 403 porque Jefatura ya no lee Mapeo por su cuenta: es lo esperado con la opción A y vuelve a confirmar la regla del dueño. Nota: `RemovePrivilegeRole` por Web API rechazó el parámetro `PrivilegeId`; los privilegios se quitaron con `DELETE roles(<id>)/roleprivileges_association(<privilegeid>)/$ref`.
