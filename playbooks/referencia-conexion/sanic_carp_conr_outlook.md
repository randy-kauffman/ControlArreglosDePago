# Playbook: referencia de conexión · CONR - CARP - Outlook

> **Tipo con 0 ciclos** (`power-platform-especificar`): la skill no tiene referencia para connection reference. El formato sale de la Identidad común y de lo que lee `herramientas/construir/referencia_conexion.py` (nueva el 2026-10-20, lote D).

## 1. Identidad

```json
{
  "tipo_playbook": "referencia-conexion",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "10.5",
  "fase": 1,
  "entorno_url": "https://org36e60d9d.crm.dynamics.com/",
  "solucion": "sanic_carp_sol_controlarreglospago",
  "publisher": "Sistemas_Abiertos_Nicaragua",
  "prefijo": "sanic",
  "abrev": "carp",
  "prefijo_opciones": 15946,
  "idiomas": [
    1033,
    3082
  ]
}
```

De dónde sale cada dato: `diseno/01-convenciones.md` §0 y §2 (patrón `sanic_carp_conr_<nombre>`, display `CONR - CARP - <Nombre>`). La referencia figura en `diseno/06-inventario-componentes.md` §10, renglón 10.5, y en `diseno/07-diseno-app-y-flujos.md` §8.1.

## 2. Qué se crea

```json
{
  "tipo": "referencia-conexion",
  "nombre": "sanic_carp_conr_outlook",
  "displayname": "CONR - CARP - Outlook",
  "descripcion": "Office 365 Outlook de la cuenta de servicio, para los avisos de los flujos CARP-EST y CARP-SAL.",
  "conector": "shared_office365",
  "conexion": null
}
```

Conector Office 365 Outlook: acción "Enviar un correo electrónico (V2)" de los avisos (`diseno/07` §8.3 y §8.4). El correo sale del buzón del dueño de la conexión, nunca del buzón del área.

**La conexión es de este entorno y NO viaja.** En Dev queda `null` a propósito: la plataforma comprueba que QUIEN crea la referencia tenga permiso sobre la conexión, y la identidad de la herramienta (service principal) no lo tiene sobre ninguna de las dos conexiones de Office 365 Outlook de la cuenta de servicio (`95bbcb03…` y `ebcf70b7…`: `ConnectionAuthorizationFailed`, 2026-10-20; con impersonación por `CallerObjectId` tampoco, porque el servicio de conectividad usa el token de la herramienta). La asocia una persona desde el portal (paso manual en `pruebas-humo/lote-d/RESULTADO.md`). Mientras no tenga conexión, la herramienta devuelve `bloqueado`; después, `ya_existia` (con `conexion: null` no compara el `connectionid`; conviene anotar el elegido en este playbook para que la verificación lo controle). En Test y Prod la conexión la crea la cuenta de servicio dedicada iniciando sesión, y se asocia a esta referencia al importar la solución o desde el portal (13.13). Si se deja `null`, la herramienta crea la referencia y devuelve `bloqueado`.

## 3. Precondiciones

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es managed, y su publisher coincide | los de la sección 1 |
| 2 | El nombre cumple `sanic_carp_conr_<nombre>`, solo ASCII | el de la sección 2 |
| 3 | La conexión existe en el entorno y es del conector `shared_office365` | la de la sección 2 |

## 4. Cómo se construye

```
python3 herramientas/construir/referencia_conexion.py playbooks/referencia-conexion/sanic_carp_conr_outlook.md
```

## 5. Verificación

```
python3 herramientas/construir/referencia_conexion.py playbooks/referencia-conexion/sanic_carp_conr_outlook.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET connectionreferences` por nombre lógico | una fila · `connectorid` `/providers/Microsoft.PowerApps/apis/shared_office365` · display y descripción de la sección 2 · `connectionid` de la sección 2 |
| 2 | Pertenencia a la solución | la referencia figura en `solutioncomponents` |
| 3 | Segunda ejecución sin `--solo-verificar` | `ya_existia` |

## 6. Si ya existe

Todas las comprobaciones; si pasan, `ya_existia`; si no, `difiere` sin tocar nada. `--completar` corrige display, descripción y conexión; el conector es inmutable.

## 7. Reversa

Borrar la referencia. Los flujos que la usan (11.1 y 11.2) dejan de poder activarse.

## 8. Fuera de alcance

- Crear la conexión (OAuth): la crea una persona iniciando sesión; no hay API.
- Compartir la conexión con otras identidades.
