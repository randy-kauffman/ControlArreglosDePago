# Playbook: clave alternativa · sanic_carp_key_conversacion_huellamessageid

## 1. Identidad

```json
{
  "tipo_playbook": "clave",
  "version_skill": "0.2.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "5.1",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09: el publisher `Sistemas_Abiertos_Nicaragua` tiene prefijo `sanic` y prefijo de opciones `15946`, la solución existe sin administrar y `RetrieveProvisionedLanguages` devuelve `[1033, 3082]`. La clave está definida en `diseno/02-diccionario-datos.md` §7 (K-01) y figura en `diseno/06-inventario-componentes.md` §5, renglón 5.1. El número de inventario sigue el orden de construcción: 1 solución, 2 choices globales, 3 tablas, 4 relaciones, 5 claves.

Advertencias: el idioma base del entorno de desarrollo es 1033. Los nombres visibles son los mismos, en español y sin tildes ni ñ (BP-PP-197), en los dos idiomas.

## 2. Qué se crea

```json
{
  "tipo": "clave",
  "nombre": "sanic_carp_key_conversacion_huellamessageid",
  "displayname": {
    "1033": "KEY - CARP - Conversacion - Huella del Message-ID",
    "3082": "KEY - CARP - Conversacion - Huella del Message-ID"
  },
  "tabla": "sanic_carp_tbl_conversacion",
  "columnas": [
    "sanic_huellamessageid"
  ]
}
```

Una clave alternativa sobre `sanic_carp_tbl_conversacion`: la plataforma no admite dos conversaciones con la misma `sanic_huellamessageid`. Es lo que garantiza que un correo reprocesado no cree dos conversaciones (diccionario §4.2, descripción de la columna).

Tamaño: `sanic_huellamessageid` es texto de 64 caracteres (SHA-256 en hexadecimal): 64 × 2 = **128 de 900 bytes**. Margen amplio; la huella existe justamente para que un Message-ID largo no supere el límite de la clave.

Valores vacíos:

La única columna de la clave, `sanic_huellamessageid`, es requerida en el diseño (playbook de la tabla 3.2: requerida `Sí`, no protegida, no auditada). No hay columna opcional que deje registros fuera de la unicidad. Pero el nivel "requerido" lo aplica la app, no el servicio: una conversación que entre por API con la huella vacía queda fuera de la unicidad. La huella la calcula y escribe el plugin de registro de correos (inventario 7.3 y 7.11), nunca el usuario.

Decisiones que este playbook toma y el diseño no traía:

| Decisión | Valor | Por qué |
|---|---|---|
| Nombre visible de la clave | «KEY - CARP - Conversacion - Huella del Message-ID» en 1033 y en 3082 | Es el que fija el diccionario §7 (K-01); sigue el patrón `KEY - CARP - Tabla - Campos` (`01` §2) y no lleva tildes ni ñ |
| Uso por URL | La clave sirve para unicidad y también para leer o hacer upsert por clave | El valor es hexadecimal en minúscula (0-9, a-f): no contiene ninguno de `< > * % & : / \ + ? #` |

Viene del diseño, no lo decide este playbook:

- Nombre, nombre visible, tabla y columna: `02` §7, fila K-01, e inventario §5, renglón 5.1.
- Condiciones de `clave.md` §4 verificadas en `02` §7: tipo texto (admitido), sin seguridad por columna, 128 de 900 bytes, tabla sin datos al crearla (sin duplicados que hagan fallar el índice).
- Comparación sin distinguir mayúsculas: la huella se escribe siempre en minúscula (`02` §4.2, T(64) SHA-256 hexadecimal, minúscula), así que no hay valores que choquen por la mayúscula.
- Origen: D-03, ítem 1.

## 3. Precondiciones

Las de la receta (`power-platform-construir`, `references/modelo-datos/patrones.md` §2.4), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es administrada, y su publisher coincide en unique name, prefijo y prefijo de opciones | los de la sección 1 |
| 2 | Cada idioma de `idiomas` está provisionado, y la lista incluye 1033 | `[1033, 3082]` |
| 3 | La tabla existe | `sanic_carp_tbl_conversacion` (inventario 3.2, construida) |
| 4 | Cada columna de la clave existe, es de un tipo que admite clave y no tiene seguridad de columna | `sanic_huellamessageid` · texto · `IsSecured = false` |
| 5 | La clave no pasa de 900 bytes (un texto ocupa 2 por carácter) | 128 bytes; la herramienta lo calcula con el largo real del entorno (64); la plataforma no lo comprueba al crear |
| 6 | Nombres visibles sin tildes ni ñ (BP-PP-197) y con el patrón `KEY - CARP - Tabla - Campos` | los de la sección 2 |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/modelo-datos/patrones.md` §2.4 "Clave alternativa". Herramienta del proyecto: `herramientas/construir/clave.py`. El índice de la clave se arma en segundo plano y tarda unos dos minutos aun con la tabla vacía (135 s en el spike 2): la herramienta espera hasta verlo `Active`.

```
python3 herramientas/construir/clave.py playbooks/clave/sanic_carp_key_conversacion_huellamessageid.md
```

## 5. Verificación

```
python3 herramientas/construir/clave.py playbooks/clave/sanic_carp_key_conversacion_huellamessageid.md --solo-verificar
python3 herramientas/construir/tabla.py playbooks/tabla/sanic_carp_tbl_conversacion.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET EntityDefinitions(LogicalName='sanic_carp_tbl_conversacion')/Keys(LogicalName='sanic_carp_key_conversacion_huellamessageid')` | 200 · `IsManaged = false` · nombre visible «KEY - CARP - Conversacion - Huella del Message-ID» en 1033 y en 3082, ninguna etiqueta en otro idioma |
| 2 | Columnas de la clave (`KeyAttributes`, sin importar el orden) | `sanic_huellamessageid`, ninguna más |
| 3 | Índice (`EntityKeyIndexStatus`) | `Active` (`Pending` o `InProgress` es "todavía no"; `Failed` es un defecto) |
| 4 | Pertenencia a la solución | la de la tabla: `sanic_carp_tbl_conversacion` está una vez en la solución de la sección 1, con todos sus subcomponentes (`rootcomponentbehavior = 0`; una clave no es un componente propio) |
| 5 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0, no cambia nada |
| 6 | La tabla sigue coincidiendo con su propio playbook | `ya_existia`: una clave no la hace diferir |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**. Un índice `Failed` también es `difiere`: casi siempre hay datos duplicados, y eso lo resuelve una persona antes de reactivar la clave (`ReactivateEntityKey`).

## 7. Reversa

`DELETE EntityDefinitions(LogicalName='sanic_carp_tbl_conversacion')/Keys(<MetadataId>)`. No borra datos ni columnas: solo deja de exigir la unicidad. **Irreversible desde que se crea**: el nombre de la clave. Las columnas de una clave no se editan: para cambiarlas se borra la clave y se crea otra. Mientras la clave exista, `sanic_huellamessageid` no se puede borrar de la tabla.

## 8. Fuera de alcance

- La huella en sí (normalizar el Message-ID y calcular el SHA-256 en hexadecimal minúscula): código de dominio, inventario 7.3.
- El plugin de registro de correos entrantes que crea la conversación y usa esta clave para no duplicarla: inventario 7.11 (steps de Email, registrados en 8.7), y la Custom API de reconciliación de correos sin conversación, inventario 7.19 y 8.3.
- La columna `sanic_huellamessageid` en sí: nace con el playbook de su tabla, inventario 3.2.
