# Playbook: clave alternativa · sanic_carp_key_versionplantilla_nombre

## 1. Identidad

```json
{
  "tipo_playbook": "clave",
  "version_skill": "0.2.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "5.3",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09: el publisher `Sistemas_Abiertos_Nicaragua` tiene prefijo `sanic` y prefijo de opciones `15946`, la solución existe sin administrar y `RetrieveProvisionedLanguages` devuelve `[1033, 3082]`. La clave está definida en `diseno/02-diccionario-datos.md` §7 (K-03) y figura en `diseno/06-inventario-componentes.md` §5, renglón 5.3. El número de inventario sigue el orden de construcción: 1 solución, 2 choices globales, 3 tablas, 4 relaciones, 5 claves.

Advertencias: el idioma base del entorno de desarrollo es 1033. Los nombres visibles son los mismos, en español y sin tildes ni ñ (BP-PP-197), en los dos idiomas.

## 2. Qué se crea

```json
{
  "tipo": "clave",
  "nombre": "sanic_carp_key_versionplantilla_nombre",
  "displayname": {
    "1033": "KEY - CARP - Version de plantilla - Numero de version",
    "3082": "KEY - CARP - Version de plantilla - Numero de version"
  },
  "tabla": "sanic_carp_tbl_versionplantilla",
  "columnas": [
    "sanic_nombre"
  ]
}
```

Una clave alternativa sobre `sanic_carp_tbl_versionplantilla`: la plataforma no admite dos versiones de plantilla con el mismo número de versión (`sanic_nombre`, por ejemplo `v07`). La columna primaria es clave de negocio, y BP-PP-192 pide que una primaria así forme parte de una clave alternativa (P-19).

Tamaño: `sanic_nombre` es texto de largo 10: 10 × 2 = **20 de 900 bytes**.

Valores vacíos:

La única columna de la clave, `sanic_nombre`, es requerida en el diseño (playbook de la tabla 3.5: requerida, `ApplicationRequired`; es la primaria). No hay columna opcional que deje registros fuera de la unicidad. Pero el nivel "requerido" lo aplica la app, no el servicio: una versión que entre por API con `sanic_nombre` vacío queda fuera de la unicidad. El diccionario (§4.5) fija el patrón `^v[0-9]{2,3}$` para el valor; quién lo hace cumplir en el servidor es diseño detallado de las piezas 7.7 y 7.16, fuera de este playbook.

Decisiones que este playbook toma y el diseño no traía:

| Decisión | Valor | Por qué |
|---|---|---|
| Uso por URL | La clave sirve para unicidad y también para leer o hacer upsert por clave | El valor sigue el patrón `^v[0-9]{2,3}$` (letra v y dígitos): no contiene ninguno de `< > * % & : / \ + ? #` |
| Mayúsculas | No hace falta normalizar | La comparación de la clave no distingue mayúsculas (`v07` y `V07` chocan), y no se busca evitarlo: el patrón del diccionario ya fija la `v` en minúscula |

Viene del diseño, no lo decide este playbook:

- Nombre, nombre visible, tabla y columna: `02` §7, fila K-03, e inventario §5, renglón 5.3.
- Condiciones de `clave.md` §4 verificadas en `02` §7: tipo texto (admitido), sin seguridad por columna, 20 de 900 bytes, tabla sin datos al crearla (sin duplicados que hagan fallar el índice).
- Origen: BP-PP-192, P-19 (aceptada el 2026-10-09). No hay clave en Cuota (P-19) ni en el resto de las tablas fuera de K-01, K-02 y K-03.

## 3. Precondiciones

Las de la receta (`power-platform-construir`, `references/modelo-datos/patrones.md` §2.4), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es administrada, y su publisher coincide en unique name, prefijo y prefijo de opciones | los de la sección 1 |
| 2 | Cada idioma de `idiomas` está provisionado, y la lista incluye 1033 | `[1033, 3082]` |
| 3 | La tabla existe | `sanic_carp_tbl_versionplantilla` (inventario 3.5, construida) |
| 4 | Cada columna de la clave existe, es de un tipo que admite clave y no tiene seguridad de columna | `sanic_nombre` · texto · `IsSecured = false` |
| 5 | La clave no pasa de 900 bytes (un texto ocupa 2 por carácter) | 20 bytes; la herramienta lo calcula con el largo real del entorno (10); la plataforma no lo comprueba al crear |
| 6 | Nombres visibles sin tildes ni ñ (BP-PP-197) y con el patrón `KEY - CARP - Tabla - Campos` | los de la sección 2 |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/modelo-datos/patrones.md` §2.4 "Clave alternativa". Herramienta del proyecto: `herramientas/construir/clave.py`. El índice de la clave se arma en segundo plano y tarda unos dos minutos aun con la tabla vacía (135 s en el spike 2): la herramienta espera hasta verlo `Active`.

```
python3 herramientas/construir/clave.py playbooks/clave/sanic_carp_key_versionplantilla_nombre.md
```

## 5. Verificación

```
python3 herramientas/construir/clave.py playbooks/clave/sanic_carp_key_versionplantilla_nombre.md --solo-verificar
python3 herramientas/construir/tabla.py playbooks/tabla/sanic_carp_tbl_versionplantilla.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET EntityDefinitions(LogicalName='sanic_carp_tbl_versionplantilla')/Keys(LogicalName='sanic_carp_key_versionplantilla_nombre')` | 200 · `IsManaged = false` · nombre visible «KEY - CARP - Version de plantilla - Numero de version» en 1033 y en 3082, ninguna etiqueta en otro idioma |
| 2 | Columnas de la clave (`KeyAttributes`, sin importar el orden) | `sanic_nombre`, ninguna más |
| 3 | Índice (`EntityKeyIndexStatus`) | `Active` (`Pending` o `InProgress` es "todavía no"; `Failed` es un defecto) |
| 4 | Pertenencia a la solución | la de la tabla: `sanic_carp_tbl_versionplantilla` está una vez en la solución de la sección 1, con todos sus subcomponentes (`rootcomponentbehavior = 0`; una clave no es un componente propio) |
| 5 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0, no cambia nada |
| 6 | La tabla sigue coincidiendo con su propio playbook | `ya_existia`: una clave no la hace diferir |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**. Un índice `Failed` también es `difiere`: casi siempre hay datos duplicados, y eso lo resuelve una persona antes de reactivar la clave (`ReactivateEntityKey`).

## 7. Reversa

`DELETE EntityDefinitions(LogicalName='sanic_carp_tbl_versionplantilla')/Keys(<MetadataId>)`. No borra datos ni columnas: solo deja de exigir la unicidad. **Irreversible desde que se crea**: el nombre de la clave. Las columnas de una clave no se editan: para cambiarlas se borra la clave y se crea otra. Mientras la clave exista, la primaria `sanic_nombre` no se puede borrar de la tabla.

## 8. Fuera de alcance

- La validación del patrón `^v[0-9]{2,3}$` y la publicación de la versión (Borrador, Vigente, Histórica): plugin de publicación, inventario 7.7 y 7.16 (steps registrados en 8.7).
- La carga de la versión v07, que es la primera fila que usa esta clave: datos semilla, inventario 9.1. La tabla está vacía al crear la clave, así que no hay duplicados que hagan fallar el índice.
- La columna `sanic_nombre` en sí: nace con el playbook de su tabla, inventario 3.5.
