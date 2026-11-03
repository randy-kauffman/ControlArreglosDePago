# Playbook: variable de entorno · EV - CARP - Modo de correo

> **Tipo con 0 ciclos** (`power-platform-especificar`). El formato es el que lee `herramientas/construir/variable_entorno.py`, igual que `sanic_carp_ev_buzonarea.md`.

## 1. Identidad

```json
{
  "tipo_playbook": "variable-entorno",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "10.9",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas). La variable sale de `diseno/08-alternativa-correo-por-flujos.md` §10 («Selección de modo por entorno»). Inventario 10.9.

Advertencias: `variable_entorno.py` maneja una sola cadena por texto visible; `idiomas` solo cumple el formato común. El `displayname` va sin tildes.

## 2. Qué se crea

```json
{
  "tipo": "variable-entorno",
  "nombre": "sanic_carp_ev_modocorreo",
  "displayname": "EV - CARP - Modo de correo",
  "descripcion": "Modo de captura y envio de correo del entorno: sss (sincronizacion del lado del servidor, por defecto) o flujos (flujos de Power Automate). Los flujos del modo flujos solo trabajan si vale flujos.",
  "tipovalor": "Texto",
  "obligatoria": true,
  "valor": "sss"
}
```

Valores admitidos: `sss` | `flujos`. **Por defecto `sss`**: el correo lo captura y envía la sincronización del lado del servidor, que es el camino principal y probado (`diseno/08` §8). El flujo `Cloud Flow - CARP - ENT - Capturar correo entrante` lee esta variable en su primer paso y, si no vale `flujos` (incluso si está vacía o con otro texto), termina en Succeeded sin hacer nada. Así la misma solución se instala en los dos modos.

**Regla: nunca SSS y flujos sobre el mismo buzón** (duplican la entrada). Pasar a `flujos` es una operación de la fase de cambio de modo (F3/F4 de `diseno/08` §8), no de este playbook: apaga la sincronización del buzón del área, carga `flujos` y enciende los flujos, en ese orden de ventana.

La definición viaja en la solución; el valor es de este entorno y NO viaja (se carga en cada entorno, 13.13). **En Dev el valor se queda en `sss`.**

## 3. Precondiciones

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es managed, y su publisher coincide | los de la sección 1 |
| 2 | El nombre cumple `sanic_carp_ev_<nombre>`, solo ASCII | el de la sección 2 |
| 3 | `displayname` sin tildes y de hasta 100 caracteres | el de la sección 2 |
| 4 | Una variable obligatoria trae valor | `sss` |

## 4. Cómo se construye

```
python3 herramientas/construir/variable_entorno.py playbooks/variable-entorno/sanic_carp_ev_modocorreo.md
```

## 5. Verificación

```
python3 herramientas/construir/variable_entorno.py playbooks/variable-entorno/sanic_carp_ev_modocorreo.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET environmentvariabledefinitions` por `schemaname` | una fila · tipo Texto · `isrequired = true` · displayname y descripción de la sección 2 |
| 2 | `GET environmentvariablevalues` por definición | un valor igual a `sss` |
| 3 | Pertenencia a la solución | la definición figura en `solutioncomponents` |
| 4 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia` |

## 6. Si ya existe

Igual que `sanic_carp_ev_buzonarea.md`: todas las comprobaciones; si pasan, `ya_existia`; si no, `difiere` sin tocar nada; `--completar` corrige lo corregible (no el tipo). Ojo: `--completar` devolvería el valor a `sss` si alguien lo cambió a `flujos` a propósito; en un entorno ya cambiado de modo, no usar `--completar`.

## 7. Reversa

Volver el valor a `sss` (y apagar los flujos ENT/SND). Borrar el valor y la definición deja el flujo ENT sin parámetro: bloquea su construcción (`bloqueado`).

## 8. Fuera de alcance

- El cambio de modo de un entorno (apagar la sincronización del buzón, cargar `flujos`, encender los flujos): F3/F4 de `diseno/08`.
- El valor en Test y Prod: se carga en cada entorno (13.13).
