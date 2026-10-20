# Playbook: variable de entorno · EV - CARP - Nombre del team de jefatura

> **Tipo con 0 ciclos** (`power-platform-especificar`): la skill no tiene referencia para variable de entorno. El formato sale de la Identidad común y de lo que lee `herramientas/construir/variable_entorno.py`.

## 1. Identidad

```json
{
  "tipo_playbook": "variable-entorno",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "10.2",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09. La variable figura en `diseno/06-inventario-componentes.md` §10, renglón 10.2, y en `diseno/03-contratos-custom-api.md` §0.4 (nombre fijado).

Advertencias: `variable_entorno.py` maneja una sola cadena por texto visible; `idiomas` solo cumple el formato común. El `displayname` va sin tildes.

## 2. Qué se crea

```json
{
  "tipo": "variable-entorno",
  "nombre": "sanic_carp_ev_nombreteamjefatura",
  "displayname": "EV - CARP - Nombre del team de jefatura",
  "descripcion": "Nombre del team de jefatura del área de arreglos de pago, buscado dentro de su unidad de negocio. Los plugins lo usan para reconocer a la jefatura (reasignar, reabrir).",
  "tipovalor": "Texto",
  "obligatoria": true,
  "valor": "Arreglos de Pago - Jefatura"
}
```

Viene del diseño: el valor `Arreglos de Pago - Jefatura` (`01-convenciones.md` §2, team de jefatura, «estable porque los plugins lo resuelven por nombre»). `ConfiguracionDelArea.TeamJefaturaId` busca el `team` con ese `name` dentro de la BU del área y exige exactamente uno (`CARP-CFG-003`); sin variable o sin valor, `CARP-CFG-006`. No hace falta para S-34 ni para el alta de clientes: solo para las operaciones de jefatura.

La definición viaja en la solución; el valor es de este entorno y NO viaja.

## 3. Precondiciones

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es managed, y su publisher coincide | los de la sección 1 |
| 2 | El nombre cumple `sanic_carp_ev_<nombre>`, solo ASCII | el de la sección 2 |
| 3 | `displayname` sin tildes y de hasta 100 caracteres | el de la sección 2 |
| 4 | Una variable obligatoria trae valor | `Arreglos de Pago - Jefatura` |

## 4. Cómo se construye

```
python3 herramientas/construir/variable_entorno.py playbooks/variable-entorno/sanic_carp_ev_nombreteamjefatura.md
```

## 5. Verificación

```
python3 herramientas/construir/variable_entorno.py playbooks/variable-entorno/sanic_carp_ev_nombreteamjefatura.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET environmentvariabledefinitions` por `schemaname` | una fila · tipo Texto · `isrequired = true` · displayname y descripción de la sección 2 |
| 2 | `GET environmentvariablevalues` por definición | un valor igual a `Arreglos de Pago - Jefatura` |
| 3 | Pertenencia a la solución | la definición figura en `solutioncomponents` |
| 4 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia` |

## 6. Si ya existe

Igual que `sanic_carp_ev_nombrebu.md`: todas las comprobaciones; si pasan, `ya_existia`; si no, `difiere` sin tocar nada; `--completar` corrige lo corregible (no el tipo).

## 7. Reversa

Borrar el valor y la definición. Sin ellos, las operaciones de jefatura fallan con `CARP-CFG-006`.

## 8. Fuera de alcance

- Crear el team `Arreglos de Pago - Jefatura` en la BU: tarea de administración (inventario 13). Si el team no existe todavía, la variable igual se carga; el plugin falla con `CARP-CFG-003` al usarla.
- El valor en Test y Prod: se carga en cada entorno.
