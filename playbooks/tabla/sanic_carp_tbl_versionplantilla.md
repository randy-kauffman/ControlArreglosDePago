# Playbook: tabla · sanic_carp_tbl_versionplantilla

## 1. Identidad

```json
{
  "tipo_playbook": "tabla",
  "version_skill": "0.2.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "3.5",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09: el publisher `Sistemas_Abiertos_Nicaragua` tiene prefijo `sanic` y prefijo de opciones `15946`, la solución existe sin administrar y `RetrieveProvisionedLanguages` devuelve `[1033, 3082]`. El componente está definido en `diseno/02-diccionario-datos.md` §4.5 (con las decisiones P-02, P-03, P-14, P-15 y P-19 de su §11). El número de inventario sigue el orden de construcción: 1 solución, 2 choices globales, 3 tablas.

Advertencias: el idioma base del entorno de desarrollo es 1033. Los nombres visibles van en español y sin tildes ni ñ (BP-PP-197), con el mismo texto en los dos idiomas. Las descripciones son prosa y llevan ortografía completa. Existe otro publisher con el prefijo `sanic` ("Sanic Corp", unique name `sanic`): el playbook fija el publisher por su unique name.

## 2. Qué se crea

```json
{
  "tipo": "tabla",
  "nombre": "sanic_carp_tbl_versionplantilla",
  "displayname": {
    "1033": "Version de plantilla",
    "3082": "Version de plantilla"
  },
  "displayname_plural": {
    "1033": "Versiones de plantilla",
    "3082": "Versiones de plantilla"
  },
  "descripcion": "Versión de la plantilla Excel del formato F-NIC-0000052 con su archivo, su huella y su estado. Es catálogo: se desactiva, nunca se purga.",
  "propiedad": "usuario",
  "notas": false,
  "actividades": false,
  "auditoria": true,
  "motivos_estado": null,
  "primaria": {
    "nombre": "sanic_nombre",
    "displayname": {
      "1033": "Numero de version",
      "3082": "Numero de version"
    },
    "descripcion": "Número de la versión de la plantilla, por ejemplo v07. Es el identificador que ve el jefe y el que se cita al hablar de un formato.",
    "largo": 10,
    "requerida": true,
    "autonumerico": ""
  },
  "columnas": [
    {
      "nombre": "sanic_archivo",
      "displayname": {
        "1033": "Archivo de plantilla",
        "3082": "Archivo de plantilla"
      },
      "descripcion": "Archivo .xlsx de la plantilla. Se guarda solo en Dataverse. Una versión que no es Borrador es inmutable, el archivo incluido.",
      "tipo": "archivo",
      "requerida": false,
      "protegida": false,
      "auditoria": false,
      "tamano_kb": 5120
    },
    {
      "nombre": "sanic_huella",
      "displayname": {
        "1033": "Huella",
        "3082": "Huella"
      },
      "descripcion": "Huella SHA-256 del archivo, en hexadecimal y minúscula, calculada al publicar la versión. El generador la valida en cada generación.",
      "tipo": "texto",
      "requerida": false,
      "protegida": false,
      "auditoria": true,
      "largo": 64
    },
    {
      "nombre": "sanic_capacidad",
      "displayname": {
        "1033": "Capacidad de cuotas",
        "3082": "Capacidad de cuotas"
      },
      "descripcion": "Filas del bloque de cuotas que admite la plantilla. Un plazo mayor se rechaza al registrar el arreglo.",
      "tipo": "entero",
      "requerida": true,
      "protegida": false,
      "auditoria": true,
      "minimo": 1,
      "maximo": 60
    },
    {
      "nombre": "sanic_estado",
      "displayname": {
        "1033": "Estado de la version",
        "3082": "Estado de la version"
      },
      "descripcion": "Borrador, Vigente o Histórica. Hay una sola Vigente a la vez; al publicar, la anterior pasa a Histórica.",
      "tipo": "choice",
      "requerida": true,
      "protegida": false,
      "auditoria": true,
      "choice": "sanic_carp_ch_estadoversionplantilla"
    },
    {
      "nombre": "sanic_fechaaprobacion",
      "displayname": {
        "1033": "Fecha de aprobacion",
        "3082": "Fecha de aprobacion"
      },
      "descripcion": "Fecha y hora de aprobación de la versión al publicarla.",
      "tipo": "fechahora",
      "requerida": false,
      "protegida": false,
      "auditoria": true
    }
  ]
}
```

### Cuenta de columnas

El diccionario (§4.5) trae **6 columnas propias no-lookup** de la tabla: la primaria `sanic_nombre` y 5 más. El bloque trae exactamente esas: la primaria en `primaria` y las 5 restantes en `columnas[]`, en el orden del diccionario. El diccionario trae además **1 lookup**, `sanic_aprobadoporid`, que no va aquí (ver sección 8).

### Decisiones que este playbook toma

Solo lo que el diccionario no trae. Nada de esto cambia un nombre lógico, un tipo, un largo, un rango, un requerida, una auditoría o una protección.

| # | Decisión | Motivo |
|---|---|---|
| 1 | Las descripciones de la tabla y de las columnas son las del diccionario, **acortadas**: se quitan las referencias internas (plugins, spikes, ítems, RNF) que el usuario no necesita ver en la ayuda de la columna. El dato que describe no cambia | Una descripción es lo que lee el usuario en la plataforma; el detalle de implementación vive en el diccionario |
| 2 | En `sanic_huella` la descripción agrega «en hexadecimal y minúscula», que el diccionario trae en la columna de tipo (`texto T(64) (SHA-256 en hexadecimal, minúscula)`) y no en la descripción | Que la ayuda de la columna diga el formato esperado |

### Viene del diseño (no es decisión de este playbook)

| Dato | Origen |
|---|---|
| Nombre lógico de la tabla y de cada columna, tipos, largos, rango de `sanic_capacidad` (1 a 60), tamaño del archivo (5120 KB) | Diccionario §4.5, §11.3, P-14 |
| Nombres visibles (singular, plural, primaria, columnas), con el mismo texto en 1033 y 3082 | Diccionario §4.5, P-17 y §0; `diseno/01-convenciones.md` §0 |
| `propiedad: usuario` | DD-01 |
| `notas: false`, `actividades: false` | Diccionario §4.5 |
| `auditoria: true` en la tabla y por columna (`Aud`): sí en huella, capacidad, estado y fecha de aprobación; **no** en el archivo | Diccionario §4.5, D-20, P-15 |
| Primaria `sanic_nombre`: clave de negocio, `requerida: true`, largo 10, sin autonumérico | Diccionario §4.5, §11.2 (P-02), BP-PP-192 |
| `motivos_estado: null`: la tabla conserva los dos motivos por defecto (Activo/Inactivo, «sin motivos propios») | Diccionario §4.5 (nota bajo la tabla de columnas) y §3.2 (solo Conversación y Arreglo tienen motivos propios) |
| Ninguna columna `protegida` | Columna `Seg` del diccionario: «No» en todas las filas; las tres columnas protegidas del proyecto están en Cliente y Arreglo (§8) |
| `sanic_archivo` no es `requerida` | Regla del formato y diccionario (§4.5: «una columna de archivo nunca es requerida») |
| El choice de `sanic_estado` es `sanic_carp_ch_estadoversionplantilla` (ya construido) | Diccionario §3.1, DD-03; `playbooks/choice-global/sanic_carp_ch_estadoversionplantilla.md` |

## 3. Precondiciones

Las de la receta (`power-platform-construir`, `references/modelo-datos/patrones.md` §2.2), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es administrada, y su publisher coincide en unique name, prefijo y prefijo de opciones | los de la sección 1 |
| 2 | Cada idioma de `idiomas` está provisionado, el idioma base del entorno está en la lista y la lista incluye 1033 | `[1033, 3082]` |
| 3 | El choice global que usa `sanic_estado` existe: `GET GlobalOptionSetDefinitions(Name='sanic_carp_ch_estadoversionplantilla')` | 200 |
| 4 | Nombres visibles sin tildes ni ñ (BP-PP-197), una entrada por idioma | los de la sección 2 |
| 5 | Todo nombre lógico cumple `sanic_carp_tbl_[a-z0-9]+` (tabla) o `sanic_[a-z0-9]+` (columna), en minúscula y de 95 caracteres o menos | `sanic_carp_tbl_versionplantilla` (31 caracteres) |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/modelo-datos/patrones.md` §2.2 «Tabla con sus columnas», con el contrato de `references/herramientas/patrones.md`. La tabla y sus columnas viajan en un solo POST; el largo y el requerida de la primaria se reenvían después de crear la tabla (la plataforma los ignora al crear). Ninguna columna de este playbook es protegida ni de fórmula: no hay pasos aparte ni pasos manuales.

```
python3 herramientas/construir/tabla.py playbooks/tabla/sanic_carp_tbl_versionplantilla.md
```

## 5. Verificación

```
python3 herramientas/construir/tabla.py playbooks/tabla/sanic_carp_tbl_versionplantilla.md --solo-verificar
python3 herramientas/construir/muestra_tabla.py playbooks/tabla/sanic_carp_tbl_versionplantilla.md --guardar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET EntityDefinitions(LogicalName='sanic_carp_tbl_versionplantilla')` | 200 · `OwnershipType = UserOwned` · `HasNotes = false` · `HasActivities = false` · `IsAuditEnabled = true` · `IsManaged = false` · `IsCustomEntity = true` · `PrimaryNameAttribute = sanic_nombre` |
| 2 | Nombre, plural y descripción | Nombre `Version de plantilla` y plural `Versiones de plantilla` en 1033 y en 3082, ninguna etiqueta en otro idioma; descripción la de la sección 2 |
| 3 | Primaria `sanic_nombre` | texto · largo 10 · requerida (`ApplicationRequired`) · no protegida · `IsAuditEnabled = true` (hereda) · nombre visible `Numero de version` en 1033 y 3082 · descripción de la sección 2 |
| 4 | `sanic_archivo` | archivo · `MaxSizeInKB = 5120` · no requerida · no protegida · `IsAuditEnabled = false` · `Archivo de plantilla` en 1033 y 3082 |
| 5 | `sanic_huella` | texto · largo 64 · no requerida · no protegida · `IsAuditEnabled = true` · `Huella` |
| 6 | `sanic_capacidad` | entero · mínimo 1 · máximo 60 · requerida · no protegida · `IsAuditEnabled = true` · `Capacidad de cuotas` |
| 7 | `sanic_estado` | choice global `sanic_carp_ch_estadoversionplantilla` · requerida · no protegida · `IsAuditEnabled = true` · `Estado de la version` |
| 8 | `sanic_fechaaprobacion` | fecha y hora, comportamiento Usuario local · no requerida · no protegida · `IsAuditEnabled = true` · `Fecha de aprobacion` |
| 9 | Ninguna columna propia de más, salvo lookups | además de las 6 columnas anteriores (primaria incluida), solo puede existir `sanic_aprobadoporid` (lookup, de su relación REL-17) |
| 10 | Pertenece a la solución de la sección 1 (tipo de componente 1) | exactamente una fila en `solutioncomponents` |
| 11 | Motivos de estado | como `motivos_estado` es `null`: `statuscode` tiene exactamente el valor 1 (estado 0) y el 2 (estado 1); `statecode` conserva exactamente los estados 0 y 1 |
| 12 | Segunda ejecución de `tabla.py` sin `--solo-verificar` | estado `ya_existia`, código de salida 0, no cambia nada |
| 13 | Muestra real: `muestra_tabla.py … --guardar` (exporta la solución y compara la tabla contra el XML) | XML en `playbooks/tabla/muestras/sanic_carp_tbl_versionplantilla.solucion.xml` con los mismos tipos, largos, rango, tamaño de archivo, requerida, protegida, auditoría y nombres |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5 (1 a 11). Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**.

## 7. Reversa

`DELETE EntityDefinitions(LogicalName='sanic_carp_tbl_versionplantilla')`, posible mientras la tabla no tenga datos ni relaciones. **Irreversible desde que se crea**: el nombre lógico de la tabla y el de cada columna; el tipo de propiedad (`usuario`); el tipo de cada columna; y el conjunto de estados `statecode`. Se puede cambiar después: nombres visibles, descripciones, requerida, auditoría, y ampliar un largo (achicarlo, no). Borrar la tabla o una columna borra sus datos, archivo incluido.

## 8. Fuera de alcance

- El lookup `sanic_aprobadoporid` (Aprobado por, `systemuser`, opcional, auditado): nace con su relación REL-17 (inventario de relaciones).
- Las relaciones en las que esta tabla es padre: REL-08 (Mapeo de celda, parental) y REL-07 (Generación de formato, restringido). Nacen con su playbook de relación.
- La clave alternativa `sanic_carp_key_versionplantilla_nombre` sobre `sanic_nombre` (P-19, K-03): playbook de clave, después de esta tabla.
- Las reglas que viven en plugins: el patrón `^v[0-9]{2,3}$` del número de versión, que `sanic_huella` y `sanic_fechaaprobacion` sean de solo-sistema (las escribe el plugin de publicación), la inmutabilidad de una versión que no es Borrador, la publicación (una sola Vigente) y el propietario team (P-11).
- El valor por defecto «Borrador» de `sanic_estado` que menciona el diccionario: el formato de columna `choice` no tiene clave de valor por defecto (ver el reporte de dudas); queda para el formulario o el plugin de alta, no para este playbook.
- Vistas, formularios, reglas de negocio, el perfil de seguridad de columna (esta tabla no tiene columnas protegidas) y los roles.
