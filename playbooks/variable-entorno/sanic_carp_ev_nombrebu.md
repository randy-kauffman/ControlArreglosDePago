# Playbook: variable de entorno · EV - CARP - Nombre de la BU

> **Tipo con 0 ciclos** (`power-platform-especificar`): la skill no tiene referencia para variable de entorno. El formato sale de la Identidad común y de lo que lee `herramientas/construir/variable_entorno.py`.

## 1. Identidad

```json
{
  "tipo_playbook": "variable-entorno",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "10.1",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09. La variable figura en `diseno/06-inventario-componentes.md` §10, renglón 10.1, y en `diseno/03-contratos-custom-api.md` §0.4.

Advertencias: `variable_entorno.py` todavía maneja una sola cadena por texto visible (no hay etiquetas por idioma); `idiomas` solo cumple el formato común. El `displayname` va sin tildes.

## 2. Qué se crea

```json
{
  "tipo": "variable-entorno",
  "nombre": "sanic_carp_ev_nombrebu",
  "displayname": "EV - CARP - Nombre de la BU",
  "descripcion": "Nombre de la unidad de negocio del área de arreglos de pago. Los plugins la usan para resolver la BU, su team propietario y el team de jefatura.",
  "tipovalor": "Texto",
  "obligatoria": true,
  "valor": "Arreglos de Pago"
}
```

Viene del diseño: el nombre (confirmado por el usuario el 2026-10-10, inconsistencia 5) y el valor `Arreglos de Pago` (`01-convenciones.md` §2, business unit hija). `ConfiguracionDelArea.BuId` busca la `businessunit` con ese `name`, no deshabilitada, y exige exactamente una (`CARP-CFG-001`). Si la variable no existe o no tiene valor, falla con `CARP-CFG-006`. Es la variable que necesitan TODOS los steps que resuelven la BU (propietario al crear, S-34 y la reconciliación).

La definición viaja en la solución; el valor es de este entorno y NO viaja (Test y Prod lo cargan de nuevo).

## 3. Precondiciones

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es managed, y su publisher coincide | los de la sección 1 |
| 2 | El nombre cumple `sanic_carp_ev_<nombre>`, solo ASCII | el de la sección 2 |
| 3 | `displayname` sin tildes y de hasta 100 caracteres | el de la sección 2 |
| 4 | Una variable obligatoria trae valor | `Arreglos de Pago` |

Si alguna falla: estado `bloqueado` o `error`, no se crea nada.

## 4. Cómo se construye

Herramienta del proyecto: `herramientas/construir/variable_entorno.py`.

```
python3 herramientas/construir/variable_entorno.py playbooks/variable-entorno/sanic_carp_ev_nombrebu.md
```

## 5. Verificación

```
python3 herramientas/construir/variable_entorno.py playbooks/variable-entorno/sanic_carp_ev_nombrebu.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET environmentvariabledefinitions` por `schemaname` | una fila · tipo Texto (100000000) · `isrequired = true` · displayname y descripción de la sección 2 |
| 2 | `GET environmentvariablevalues` por definición | un valor igual a `Arreglos de Pago` |
| 3 | Pertenencia a la solución | la definición figura en `solutioncomponents` de la solución de la sección 1 |
| 4 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia` |

## 6. Si ya existe

Se hacen todas las comprobaciones. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, sin tocar nada. `--completar` corrige displayname, descripción, obligatoriedad y valor; el tipo no se puede cambiar.

## 7. Reversa

Borrar el valor (`environmentvariablevalues`) y la definición (`environmentvariabledefinitions`). Sin valor, toda alta hecha por una persona falla con `CARP-CFG-006`.

## 8. Fuera de alcance

- El valor en Test y Prod: se carga en cada entorno (inventario 13).
- La BU `Arreglos de Pago` y su team propietario: ya existen (`01-convenciones.md` §2).
