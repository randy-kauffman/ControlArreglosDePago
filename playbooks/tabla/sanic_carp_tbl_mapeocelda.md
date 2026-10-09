# Playbook: tabla · sanic_carp_tbl_mapeocelda

## 1. Identidad

```json
{
  "tipo_playbook": "tabla",
  "version_skill": "0.2.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "3.6",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09: el publisher `Sistemas_Abiertos_Nicaragua` tiene prefijo `sanic` y prefijo de opciones `15946`, la solución existe sin administrar y `RetrieveProvisionedLanguages` devuelve `[1033, 3082]`. El componente está definido en `diseno/02-diccionario-datos.md` §4.6 (con las decisiones P-02, P-03 y P-15 de su §11). El número de inventario sigue el orden de construcción: 1 solución, 2 choices globales, 3 tablas.

Advertencias: el idioma base del entorno de desarrollo es 1033. Los nombres visibles van en español y sin tildes ni ñ (BP-PP-197), con el mismo texto en los dos idiomas. Las descripciones son prosa y llevan ortografía completa. Existe otro publisher con el prefijo `sanic` ("Sanic Corp", unique name `sanic`): el playbook fija el publisher por su unique name.

## 2. Qué se crea

```json
{
  "tipo": "tabla",
  "nombre": "sanic_carp_tbl_mapeocelda",
  "displayname": {
    "1033": "Mapeo de celda",
    "3082": "Mapeo de celda"
  },
  "displayname_plural": {
    "1033": "Mapeos de celda",
    "3082": "Mapeos de celda"
  },
  "descripcion": "Fila del mapeo celda → campo de una versión de plantilla: qué dato del arreglo se escribe en qué celda del Excel.",
  "propiedad": "usuario",
  "notas": false,
  "actividades": false,
  "auditoria": true,
  "motivos_estado": null,
  "primaria": {
    "nombre": "sanic_nombre",
    "displayname": {
      "1033": "Hoja y celda",
      "3082": "Hoja y celda"
    },
    "descripcion": "Hoja y celda del mapeo, con la forma hoja!celda. La calcula el sistema; nadie la digita.",
    "largo": 50,
    "requerida": true,
    "autonumerico": ""
  },
  "columnas": [
    {
      "nombre": "sanic_hoja",
      "displayname": {
        "1033": "Hoja",
        "3082": "Hoja"
      },
      "descripcion": "Nombre exacto de la hoja del libro; se compara tal cual. La hoja del formato se llama ' FORMATO ARP ELECTRONICO', con un espacio al inicio. El límite de Excel para un nombre de hoja es 31 caracteres.",
      "tipo": "texto",
      "requerida": true,
      "protegida": false,
      "auditoria": true,
      "largo": 31
    },
    {
      "nombre": "sanic_celda",
      "displayname": {
        "1033": "Celda",
        "3082": "Celda"
      },
      "descripcion": "Celda en notación A1. Para un bloque de cuotas, la celda de la primera cuota.",
      "tipo": "texto",
      "requerida": true,
      "protegida": false,
      "auditoria": true,
      "largo": 10
    },
    {
      "nombre": "sanic_tipo",
      "displayname": {
        "1033": "Tipo de mapeo",
        "3082": "Tipo de mapeo"
      },
      "descripcion": "Valor simple, total calculado o columna de bloque de cuotas.",
      "tipo": "choice",
      "requerida": true,
      "protegida": false,
      "auditoria": true,
      "choice": "sanic_carp_ch_tipomapeo"
    },
    {
      "nombre": "sanic_campo",
      "displayname": {
        "1033": "Campo de origen",
        "3082": "Campo de origen"
      },
      "descripcion": "Dato del arreglo que se escribe en la celda. La lista la mantiene el equipo de la solución.",
      "tipo": "choice",
      "requerida": true,
      "protegida": false,
      "auditoria": true,
      "choice": "sanic_carp_ch_campomapeable"
    },
    {
      "nombre": "sanic_filasmaximas",
      "displayname": {
        "1033": "Filas maximas",
        "3082": "Filas maximas"
      },
      "descripcion": "Filas máximas del bloque de cuotas a partir de la celda inicial. Solo se usa en las columnas de bloque.",
      "tipo": "entero",
      "requerida": false,
      "protegida": false,
      "auditoria": true,
      "minimo": 1,
      "maximo": 60
    }
  ]
}
```

### Cuenta de columnas

El diccionario (§4.6) trae **6 columnas propias no-lookup** de la tabla: la primaria `sanic_nombre` y 5 más. El bloque trae exactamente esas: la primaria en `primaria` y las 5 restantes en `columnas[]`, en el orden del diccionario. El diccionario trae además **1 lookup**, `sanic_versionplantillaid`, que no va aquí (ver sección 8).

### Decisiones que este playbook toma

Solo lo que el diccionario no trae. Nada de esto cambia un nombre lógico, un tipo, un largo, un rango, un requerida, una auditoría o una protección.

| # | Decisión | Motivo |
|---|---|---|
| 1 | Las descripciones de la tabla y de las columnas son las del diccionario, **acortadas**: se quitan las referencias internas (plugin de publicación, spikes, ítems) que el usuario no necesita ver en la ayuda de la columna. El dato que describe no cambia | Una descripción es lo que lee el usuario en la plataforma; el detalle de implementación vive en el diccionario |
| 2 | En `sanic_hoja` el nombre de la hoja del formato se escribe entre comillas simples en la descripción para que se vea el espacio inicial | Que quien lea la ayuda no «corrija» el espacio |

### Viene del diseño (no es decisión de este playbook)

| Dato | Origen |
|---|---|
| Nombre lógico de la tabla y de cada columna, tipos, largos (hoja 31, celda 10, primaria 50), rango de `sanic_filasmaximas` (1 a 60) | Diccionario §4.6, §11.2, §11.3 |
| Nombres visibles (singular, plural, primaria, columnas), con el mismo texto en 1033 y 3082. La primaria se llama `Hoja y celda` para no repetir el nombre de `sanic_celda` (`Celda`) | Diccionario §4.6 (corregido el 2026-10-09), P-17 y §0; `diseno/01-convenciones.md` §0 |
| `propiedad: usuario` | DD-01 |
| `notas: false`, `actividades: false` | Diccionario §4.6 |
| `auditoria: true` en la tabla y **todas** sus columnas | Diccionario §4.6 (D-20: «registra quién cambió el mapeo y cuándo»), P-15 |
| Primaria `sanic_nombre`: calculada por plugin con la forma `<hoja>!<celda>`, **`requerida: true`** (sigue siendo requerida: en el formulario va en solo lectura u oculta, BP-PP-192), largo 50, sin autonumérico | Diccionario §4.6, §11.2 (P-02) |
| `sanic_filasmaximas` no es `requerida` (solo aplica a columnas de bloque) | Diccionario §4.6 |
| `motivos_estado: null`: la tabla conserva los dos motivos por defecto | Diccionario §3.2 (solo Conversación y Arreglo tienen motivos propios) |
| Ninguna columna `protegida` | Columna `Seg` del diccionario: «No» en todas las filas |
| Los choices de `sanic_tipo` y `sanic_campo` son `sanic_carp_ch_tipomapeo` y `sanic_carp_ch_campomapeable` (ya construidos) | Diccionario §3.1, §4.6; `playbooks/choice-global/` |

## 3. Precondiciones

Las de la receta (`power-platform-construir`, `references/modelo-datos/patrones.md` §2.2), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es administrada, y su publisher coincide en unique name, prefijo y prefijo de opciones | los de la sección 1 |
| 2 | Cada idioma de `idiomas` está provisionado, el idioma base del entorno está en la lista y la lista incluye 1033 | `[1033, 3082]` |
| 3 | Los dos choices globales que usan las columnas existen: `GET GlobalOptionSetDefinitions(Name='sanic_carp_ch_tipomapeo')` y `GET GlobalOptionSetDefinitions(Name='sanic_carp_ch_campomapeable')` | 200 en las dos |
| 4 | Nombres visibles sin tildes ni ñ (BP-PP-197), una entrada por idioma | los de la sección 2 |
| 5 | Todo nombre lógico cumple `sanic_carp_tbl_[a-z0-9]+` (tabla) o `sanic_[a-z0-9]+` (columna), en minúscula y de 95 caracteres o menos | `sanic_carp_tbl_mapeocelda` (25 caracteres) |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/modelo-datos/patrones.md` §2.2 «Tabla con sus columnas», con el contrato de `references/herramientas/patrones.md`. La tabla y sus columnas viajan en un solo POST; el largo y el requerida de la primaria se reenvían después de crear la tabla (la plataforma los ignora al crear). Ninguna columna de este playbook es protegida ni de fórmula: no hay pasos aparte ni pasos manuales.

```
python3 herramientas/construir/tabla.py playbooks/tabla/sanic_carp_tbl_mapeocelda.md
```

## 5. Verificación

```
python3 herramientas/construir/tabla.py playbooks/tabla/sanic_carp_tbl_mapeocelda.md --solo-verificar
python3 herramientas/construir/muestra_tabla.py playbooks/tabla/sanic_carp_tbl_mapeocelda.md --guardar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET EntityDefinitions(LogicalName='sanic_carp_tbl_mapeocelda')` | 200 · `OwnershipType = UserOwned` · `HasNotes = false` · `HasActivities = false` · `IsAuditEnabled = true` · `IsManaged = false` · `IsCustomEntity = true` · `PrimaryNameAttribute = sanic_nombre` |
| 2 | Nombre, plural y descripción | Nombre `Mapeo de celda` y plural `Mapeos de celda` en 1033 y en 3082, ninguna etiqueta en otro idioma; descripción la de la sección 2 |
| 3 | Primaria `sanic_nombre` | texto · largo 50 · requerida (`ApplicationRequired`) · no protegida · `IsAuditEnabled = true` (hereda) · nombre visible `Hoja y celda` en 1033 y 3082 · descripción de la sección 2 |
| 4 | `sanic_hoja` | texto · largo 31 · requerida · no protegida · `IsAuditEnabled = true` · `Hoja` |
| 5 | `sanic_celda` | texto · largo 10 · requerida · no protegida · `IsAuditEnabled = true` · `Celda` |
| 6 | `sanic_tipo` | choice global `sanic_carp_ch_tipomapeo` · requerida · no protegida · `IsAuditEnabled = true` · `Tipo de mapeo` |
| 7 | `sanic_campo` | choice global `sanic_carp_ch_campomapeable` · requerida · no protegida · `IsAuditEnabled = true` · `Campo de origen` |
| 8 | `sanic_filasmaximas` | entero · mínimo 1 · máximo 60 · no requerida · no protegida · `IsAuditEnabled = true` · `Filas maximas` |
| 9 | Ninguna columna propia de más, salvo lookups | además de las 6 columnas anteriores (primaria incluida), solo puede existir `sanic_versionplantillaid` (lookup, de su relación REL-08) |
| 10 | Pertenece a la solución de la sección 1 (tipo de componente 1) | exactamente una fila en `solutioncomponents` |
| 11 | Motivos de estado | como `motivos_estado` es `null`: `statuscode` tiene exactamente el valor 1 (estado 0) y el 2 (estado 1); `statecode` conserva exactamente los estados 0 y 1 |
| 12 | Segunda ejecución de `tabla.py` sin `--solo-verificar` | estado `ya_existia`, código de salida 0, no cambia nada |
| 13 | Muestra real: `muestra_tabla.py … --guardar` (exporta la solución y compara la tabla contra el XML) | XML en `playbooks/tabla/muestras/sanic_carp_tbl_mapeocelda.solucion.xml` con los mismos tipos, largos, rango, requerida, protegida, auditoría y nombres |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5 (1 a 11). Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**.

## 7. Reversa

`DELETE EntityDefinitions(LogicalName='sanic_carp_tbl_mapeocelda')`, posible mientras la tabla no tenga datos ni relaciones. **Irreversible desde que se crea**: el nombre lógico de la tabla y el de cada columna; el tipo de propiedad (`usuario`); el tipo de cada columna; y el conjunto de estados `statecode`. Se puede cambiar después: nombres visibles, descripciones, requerida, auditoría, y ampliar un largo (achicarlo, no). Borrar la tabla o una columna borra sus datos.

## 8. Fuera de alcance

- El lookup `sanic_versionplantillaid` (Version de plantilla, requerido, auditado): nace con su relación REL-08 (parental), cuyo padre es `sanic_carp_tbl_versionplantilla` (inventario 3.5, que se construye antes).
- Las reglas que viven en plugins: el cálculo de la primaria `<hoja>!<celda>`, la normalización de la celda en mayúscula, que la celda esté bien formada y que no haya dos campos en la misma celda, que el tipo de mapeo admitido corresponda al campo (columna «tipo de mapeo que admite» del diccionario §3.1), que un mapeo solo se borre en una versión Borrador, y el propietario team (P-11).
- Vistas, formularios (la primaria va en solo lectura u oculta, BP-PP-192), reglas de negocio, perfil de seguridad de columna (esta tabla no tiene columnas protegidas) y roles.
