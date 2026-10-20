# Playbook: variable de entorno · EV - CARP - Destinatarios de alertas

> **Tipo con 0 ciclos** (`power-platform-especificar`): la skill no tiene referencia para variable de entorno. El formato sale de la Identidad común y de lo que lee `herramientas/construir/variable_entorno.py`.

## 1. Identidad

```json
{
  "tipo_playbook": "variable-entorno",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "10.3",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09. La variable figura en `diseno/06-inventario-componentes.md` §10, renglón 10.3, y su contenido en `diseno/07` §8.2.

Advertencias: `variable_entorno.py` maneja una sola cadena por texto visible; `idiomas` solo cumple el formato común. El `displayname` va sin tildes.

## 2. Qué se crea

```json
{
  "tipo": "variable-entorno",
  "nombre": "sanic_carp_ev_destinatariosalertas",
  "displayname": "EV - CARP - Destinatarios de alertas",
  "descripcion": "Direcciones de correo, separadas por punto y coma, que reciben los avisos de los flujos de monitoreo del buzón del área (CARP-EST y CARP-SAL).",
  "tipovalor": "Texto",
  "obligatoria": true,
  "valor": "buzondevpp3@55xljh.onmicrosoft.com"
}
```

Viene de `diseno/07-diseno-app-y-flujos.md` §8.2 (lista separada por `;`, campo "Para" de todos los avisos). **Solo en Dev** el valor es el buzón de prueba 3 (`PP_MAIL_BUZON_3`): ni el 1 (es el buzón del área; un aviso a sí mismo entraría como correo entrante y abriría una conversación) ni el 2 (hace de cliente en la tanda 3). Regla del proyecto: en Dev, avisos solo a los buzones de prueba 1 a 4 (decisión del orquestador del 2026-10-20). **En Prod el valor es el del jefe y el equipo de automatización, que nunca se carga desde este repositorio.**

La definición viaja en la solución; el valor es de este entorno y NO viaja (se carga en cada entorno, 13.13).

## 3. Precondiciones

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es managed, y su publisher coincide | los de la sección 1 |
| 2 | El nombre cumple `sanic_carp_ev_<nombre>`, solo ASCII | el de la sección 2 |
| 3 | `displayname` sin tildes y de hasta 100 caracteres | el de la sección 2 |
| 4 | Una variable obligatoria trae valor | el de la sección 2 |

## 4. Cómo se construye

```
python3 herramientas/construir/variable_entorno.py playbooks/variable-entorno/sanic_carp_ev_destinatariosalertas.md
```

## 5. Verificación

```
python3 herramientas/construir/variable_entorno.py playbooks/variable-entorno/sanic_carp_ev_destinatariosalertas.md --solo-verificar
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

Borrar el valor y la definición. Sin valor, los flujos 11.1 y 11.2 no se pueden activar (`XrmEnvironmentVariableAttributeNotFound`).

## 8. Fuera de alcance

- Los flujos que la leen (11.1 y 11.2, `playbooks/flujo/`).
- El valor en Test y Prod: se carga en cada entorno (13.13).
