# Playbook: variable de entorno · EV - CARP - Buzon del area

> **Tipo con 0 ciclos** (`power-platform-especificar`): la skill no tiene referencia para variable de entorno. El formato sale de la Identidad común y de lo que lee `herramientas/construir/variable_entorno.py`.

## 1. Identidad

```json
{
  "tipo_playbook": "variable-entorno",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "10.8",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09. La variable figura en `diseno/06-inventario-componentes.md` §10, renglón 10.8, y en `diseno/03-contratos-custom-api.md` §0.4 (nombre fijado, decisión del usuario del 2026-10-09, inconsistencia 4).

Advertencias: `variable_entorno.py` maneja una sola cadena por texto visible; `idiomas` solo cumple el formato común. El `displayname` va sin tildes.

## 2. Qué se crea

```json
{
  "tipo": "variable-entorno",
  "nombre": "sanic_carp_ev_buzonarea",
  "displayname": "EV - CARP - Buzon del area",
  "descripcion": "Dirección de correo del buzón compartido del área de arreglos de pago. Los plugins la usan para resolver la queue remitente de las respuestas.",
  "tipovalor": "Texto",
  "obligatoria": true,
  "valor": "buzondevpp1@55xljh.onmicrosoft.com"
}
```

Viene del diseño: el nombre (`03-contratos-custom-api.md` §0.4) y, **solo en Dev**, el valor del buzón de prueba 1 (`PP_MAIL_BUZON_1`, el mismo que usaron los spikes de sincronización; aprobado por el usuario el 2026-10-20). `ConfiguracionDelArea.QueueRemitenteId` busca la `queue` con `emailaddress` igual al valor (sin distinguir mayúsculas) y `statecode` 0, y exige exactamente una: cero ⇒ `CARP-CFG-004`, más de una ⇒ `CARP-CFG-005`; sin variable o sin valor, `CARP-CFG-006`. La exigen los correos salientes (responder, iniciar una conversación nueva); S-34 y el alta de clientes no la usan.

La definición viaja en la solución; el valor es de este entorno y NO viaja. **En Prod el valor es el buzón real del área, que nunca se carga desde este repositorio.**

## 3. Precondiciones

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es managed, y su publisher coincide | los de la sección 1 |
| 2 | El nombre cumple `sanic_carp_ev_<nombre>`, solo ASCII | el de la sección 2 |
| 3 | `displayname` sin tildes y de hasta 100 caracteres | el de la sección 2 |
| 4 | Una variable obligatoria trae valor | la dirección del buzón 1 |

## 4. Cómo se construye

```
python3 herramientas/construir/variable_entorno.py playbooks/variable-entorno/sanic_carp_ev_buzonarea.md
```

## 5. Verificación

```
python3 herramientas/construir/variable_entorno.py playbooks/variable-entorno/sanic_carp_ev_buzonarea.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET environmentvariabledefinitions` por `schemaname` | una fila · tipo Texto · `isrequired = true` · displayname y descripción de la sección 2 |
| 2 | `GET environmentvariablevalues` por definición | un valor igual al de la sección 2 |
| 3 | Pertenencia a la solución | la definición figura en `solutioncomponents` |
| 4 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia` |

## 6. Si ya existe

Igual que `sanic_carp_ev_nombrebu.md`: todas las comprobaciones; si pasan, `ya_existia`; si no, `difiere` sin tocar nada; `--completar` corrige lo corregible (no el tipo).

## 7. Reversa

Borrar el valor y la definición. Sin ellos, todo correo saliente falla con `CARP-CFG-006`.

## 8. Fuera de alcance

- La queue `Arreglos de Pago` y su Mailbox (inventario 13.4 y 13.5): tarea de administración. Si la queue no existe todavía, la variable igual se carga; el plugin falla con `CARP-CFG-004` al responder.
- El valor en Test y Prod: se carga en cada entorno (13.13).
