# Playbook: clave alternativa · sanic_carp_key_arreglo_producto_numeroproducto_numeroarreglo

## 1. Identidad

```json
{
  "tipo_playbook": "clave",
  "version_skill": "0.2.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "5.2",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09: el publisher `Sistemas_Abiertos_Nicaragua` tiene prefijo `sanic` y prefijo de opciones `15946`, la solución existe sin administrar y `RetrieveProvisionedLanguages` devuelve `[1033, 3082]`. La clave está definida en `diseno/02-diccionario-datos.md` §7 (K-02) y figura en `diseno/06-inventario-componentes.md` §5, renglón 5.2. El número de inventario sigue el orden de construcción: 1 solución, 2 choices globales, 3 tablas, 4 relaciones, 5 claves.

Advertencias: el idioma base del entorno de desarrollo es 1033. Los nombres visibles son los mismos, en español y sin tildes ni ñ (BP-PP-197), en los dos idiomas.

## 2. Qué se crea

```json
{
  "tipo": "clave",
  "nombre": "sanic_carp_key_arreglo_producto_numeroproducto_numeroarreglo",
  "displayname": {
    "1033": "KEY - CARP - Arreglo - Producto, numero de producto y numero de arreglo",
    "3082": "KEY - CARP - Arreglo - Producto, numero de producto y numero de arreglo"
  },
  "tabla": "sanic_carp_tbl_arreglo",
  "columnas": [
    "sanic_producto",
    "sanic_numeroproducto",
    "sanic_numeroarreglo"
  ]
}
```

Una clave alternativa sobre `sanic_carp_tbl_arreglo`: la plataforma no admite dos arreglos con la misma combinación de `sanic_producto` + `sanic_numeroproducto` + `sanic_numeroarreglo`. Es la barrera del consecutivo por crédito (RF-07): dos formalizaciones simultáneas del mismo crédito no pueden tomar el mismo número de arreglo.

Tamaño: 4 (choice `sanic_producto`) + 30 × 2 (texto `sanic_numeroproducto`, largo 30) + 4 (entero `sanic_numeroarreglo`) = **68 de 900 bytes**.

Valores vacíos (la advertencia que exige `clave.md` §4):

- **`sanic_numeroarreglo` es opcional, y es deliberado.** Está vacío en todo borrador y lo fija el sistema al formalizar. Mientras esté vacío, la plataforma no exige unicidad: dos borradores del mismo crédito son válidos para la plataforma. La clave no protege a los borradores entre sí (R-07). El spike 2 confirmó los tres comportamientos: admite varios borradores con número vacío para el mismo crédito, rechaza con 412 el duplicado con número y rechaza la segunda de dos formalizaciones simultáneas.
- `sanic_producto` y `sanic_numeroproducto` son requeridas en el diseño, pero el nivel "requerido" lo aplica la app, no el servicio: por API pueden llegar vacías, y un arreglo con una de las dos vacías queda fuera de la unicidad. Este playbook no las cambia: quién las rechaza es el plugin de registro del borrador (inventario 7.14).

Comparación sin distinguir mayúsculas ni acentos: `ABC` y `abc` chocan, y `5412 7500 1234 8890` frente a `5412750012348890` serían dos créditos distintos para la clave, rompiendo el consecutivo. Por eso el diseño normaliza el número de producto antes de guardarlo (sin espacios y en mayúscula; P-06, inventario 7.3); el playbook de la tabla 3.3 no lo hace, lo hace el plugin.

Uso por URL: un número de producto con alguno de `< > * % & : / \ + ? #` sirve para unicidad pero **no** para leer ni hacer upsert por clave en la URL. El diseño no hace upsert por clave (usa una consulta `max+1`, RF-07): queda **prohibido** hacerlo.

Decisiones que este playbook toma y el diseño no traía:

| Decisión | Valor | Por qué |
|---|---|---|
| Orden de las columnas en el bloque | `sanic_producto`, `sanic_numeroproducto`, `sanic_numeroarreglo` | Es el del diccionario §7 y el del nombre de la clave; el orden no define la unicidad y la plataforma devuelve las columnas en el suyo (la verificación lo ignora) |

Viene del diseño, no lo decide este playbook:

- Nombre, nombre visible, tabla y columnas: `02` §7, fila K-02, e inventario §5, renglón 5.2.
- Condiciones de `clave.md` §4 verificadas en `02` §7: tipos admitidos (choice, texto, entero), ninguna columna con seguridad por columna (`sanic_numeroproducto` no la lleva por D-29), 68 de 900 bytes, tabla sin datos al crearla.
- Si el plan B de D-29 se activa (seguridad por columna sobre el número de producto), esta clave cambia: una columna protegida no puede ir en una clave (R-09, pendiente de Cumplimiento).
- Origen: RF-07, D-09, D-33.

## 3. Precondiciones

Las de la receta (`power-platform-construir`, `references/modelo-datos/patrones.md` §2.4), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es administrada, y su publisher coincide en unique name, prefijo y prefijo de opciones | los de la sección 1 |
| 2 | Cada idioma de `idiomas` está provisionado, y la lista incluye 1033 | `[1033, 3082]` |
| 3 | La tabla existe | `sanic_carp_tbl_arreglo` (inventario 3.3, construida) |
| 4 | Cada columna de la clave existe, es de un tipo que admite clave y no tiene seguridad de columna | `sanic_producto` (choice) · `sanic_numeroproducto` (texto) · `sanic_numeroarreglo` (entero) · las tres con `IsSecured = false` |
| 5 | La clave no pasa de 900 bytes (un texto ocupa 2 por carácter) | 68 bytes; la herramienta lo calcula con el largo real del entorno (30); la plataforma no lo comprueba al crear |
| 6 | El choice de `sanic_producto` existe | `sanic_carp_ch_productocrediticio` (inventario 2.3, hecho) |
| 7 | Nombres visibles sin tildes ni ñ (BP-PP-197) y con el patrón `KEY - CARP - Tabla - Campos` | los de la sección 2 |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/modelo-datos/patrones.md` §2.4 "Clave alternativa". Herramienta del proyecto: `herramientas/construir/clave.py`. El índice de la clave se arma en segundo plano y tarda unos dos minutos aun con la tabla vacía (135 s en el spike 2): la herramienta espera hasta verlo `Active`.

```
python3 herramientas/construir/clave.py playbooks/clave/sanic_carp_key_arreglo_producto_numeroproducto_numeroarreglo.md
```

## 5. Verificación

```
python3 herramientas/construir/clave.py playbooks/clave/sanic_carp_key_arreglo_producto_numeroproducto_numeroarreglo.md --solo-verificar
python3 herramientas/construir/tabla.py playbooks/tabla/sanic_carp_tbl_arreglo.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET EntityDefinitions(LogicalName='sanic_carp_tbl_arreglo')/Keys(LogicalName='sanic_carp_key_arreglo_producto_numeroproducto_numeroarreglo')` | 200 · `IsManaged = false` · nombre visible «KEY - CARP - Arreglo - Producto, numero de producto y numero de arreglo» en 1033 y en 3082, ninguna etiqueta en otro idioma |
| 2 | Columnas de la clave (`KeyAttributes`, sin importar el orden) | `sanic_producto`, `sanic_numeroproducto`, `sanic_numeroarreglo`, ninguna más |
| 3 | Índice (`EntityKeyIndexStatus`) | `Active` (`Pending` o `InProgress` es "todavía no"; `Failed` es un defecto) |
| 4 | Pertenencia a la solución | la de la tabla: `sanic_carp_tbl_arreglo` está una vez en la solución de la sección 1, con todos sus subcomponentes (`rootcomponentbehavior = 0`; una clave no es un componente propio) |
| 5 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0, no cambia nada |
| 6 | La tabla sigue coincidiendo con su propio playbook | `ya_existia`: una clave no la hace diferir |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**. Un índice `Failed` también es `difiere`: casi siempre hay datos duplicados, y eso lo resuelve una persona antes de reactivar la clave (`ReactivateEntityKey`).

## 7. Reversa

`DELETE EntityDefinitions(LogicalName='sanic_carp_tbl_arreglo')/Keys(<MetadataId>)`. No borra datos ni columnas: solo deja de exigir la unicidad. **Irreversible desde que se crea**: el nombre de la clave. Las columnas de una clave no se editan: para cambiarlas se borra la clave y se crea otra. Mientras la clave exista, `sanic_producto`, `sanic_numeroproducto` y `sanic_numeroarreglo` no se pueden borrar de la tabla, ni se puede activar seguridad de columna sobre ellas.

## 8. Fuera de alcance

- La normalización del número de producto: código de dominio, inventario 7.3.
- El consecutivo por crédito (`max+1`) y su comparación con el número provisional impreso: código de dominio, inventario 7.5; la formalización que fija `sanic_numeroarreglo` y usa esta clave como barrera: steps de Arreglo, inventario 7.14 (registrados en 8.7).
- Las columnas de la clave: nacen con el playbook de su tabla, inventario 3.3; el choice del producto, inventario 2.3.
- La decisión de Cumplimiento sobre el tratamiento del número de producto (R-09): si activa el plan B de D-29, esta clave se rehace.
