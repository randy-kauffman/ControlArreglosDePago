# Playbook: variable de entorno · EV - CARP - Umbrales de monitoreo

> **Tipo con 0 ciclos** (`power-platform-especificar`): la skill no tiene referencia para variable de entorno. El formato sale de la Identidad común y de lo que lee `herramientas/construir/variable_entorno.py`.

## 1. Identidad

```json
{
  "tipo_playbook": "variable-entorno",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "10.4",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09. La variable figura en `diseno/06-inventario-componentes.md` §10, renglón 10.4, y su contenido en `diseno/07` §8.2.

Advertencias: `variable_entorno.py` maneja una sola cadena por texto visible; `idiomas` solo cumple el formato común. El `displayname` va sin tildes.

## 2. Qué se crea

```json
{
  "tipo": "variable-entorno",
  "nombre": "sanic_carp_ev_umbralesmonitoreo",
  "displayname": "EV - CARP - Umbrales de monitoreo",
  "descripcion": "Umbrales del flujo de salud del buzón (CARP-SAL), en JSON: horas sin correo entrante, ventana de errores de envío en minutos, horas hacia atrás para purgas fallidas y minutos mínimos para reconciliar correos.",
  "tipovalor": "JSON",
  "obligatoria": true,
  "valor": "{\"horasSinEntrada\":4,\"ventanaErroresMin\":35,\"horasPurga\":24,\"minutosReconciliar\":10}"
}
```

Viene de `diseno/07-diseno-app-y-flujos.md` §8.2: una sola variable JSON en vez de cuatro. `horasSinEntrada` = 4 es el valor por defecto del diseño, **a confirmar con el jefe** (DA-03). El mismo valor sirve en Dev, Test y Prod salvo que el jefe pida otro.

La definición viaja en la solución; el valor es de este entorno y NO viaja (se carga en cada entorno, 13.13).

## 3. Precondiciones

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es managed, y su publisher coincide | los de la sección 1 |
| 2 | El nombre cumple `sanic_carp_ev_<nombre>`, solo ASCII | el de la sección 2 |
| 3 | `displayname` sin tildes y de hasta 100 caracteres | el de la sección 2 |
| 4 | Una variable obligatoria trae valor y el valor es JSON válido con las cuatro claves | el de la sección 2 |

## 4. Cómo se construye

```
python3 herramientas/construir/variable_entorno.py playbooks/variable-entorno/sanic_carp_ev_umbralesmonitoreo.md
```

## 5. Verificación

```
python3 herramientas/construir/variable_entorno.py playbooks/variable-entorno/sanic_carp_ev_umbralesmonitoreo.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET environmentvariabledefinitions` por `schemaname` | una fila · tipo JSON · `isrequired = true` · displayname y descripción de la sección 2 |
| 2 | `GET environmentvariablevalues` por definición | un valor igual al de la sección 2 (texto idéntico) |
| 3 | Pertenencia a la solución | la definición figura en `solutioncomponents` |
| 4 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia` |

## 6. Si ya existe

Igual que `sanic_carp_ev_nombrebu.md`: todas las comprobaciones; si pasan, `ya_existia`; si no, `difiere` sin tocar nada; `--completar` corrige lo corregible (no el tipo).

## 7. Reversa

Borrar el valor y la definición. Sin valor, el flujo 11.2 no se puede activar.

## 8. Fuera de alcance

- El flujo que la lee (11.2, `playbooks/flujo/carp-sal.md`).
- El umbral de 30 días de los borradores sin movimiento: es la vista 12.6, no esta variable.
