# Playbook: tabla · sanic_carp_tbl_generacionformato

## 1. Identidad

```json
{
  "tipo_playbook": "tabla",
  "version_skill": "0.2.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "3.7",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09: el publisher `Sistemas_Abiertos_Nicaragua` tiene prefijo `sanic` y prefijo de opciones `15946`, la solución existe sin administrar y `RetrieveProvisionedLanguages` devuelve `[1033, 3082]`. El componente está definido en `diseno/02-diccionario-datos.md` §4.7 (con las decisiones P-02 y P-15 de su §11). El número de inventario sigue el orden de construcción: 1 solución, 2 choices globales, 3 tablas.

Advertencias: el idioma base del entorno de desarrollo es 1033. Los nombres visibles van en español y sin tildes ni ñ (BP-PP-197), con el mismo texto en los dos idiomas. Las descripciones son prosa y llevan ortografía completa. Existe otro publisher con el prefijo `sanic` ("Sanic Corp", unique name `sanic`): el playbook fija el publisher por su unique name.

## 2. Qué se crea

```json
{
  "tipo": "tabla",
  "nombre": "sanic_carp_tbl_generacionformato",
  "displayname": {
    "1033": "Generacion de formato",
    "3082": "Generacion de formato"
  },
  "displayname_plural": {
    "1033": "Generaciones de formato",
    "3082": "Generaciones de formato"
  },
  "descripcion": "Bitácora de cada generación del formato F-NIC-0000052: qué arreglo, con qué versión de plantilla, quién y cuándo. El archivo generado nunca se guarda.",
  "propiedad": "usuario",
  "notas": false,
  "actividades": false,
  "auditoria": true,
  "motivos_estado": null,
  "primaria": {
    "nombre": "sanic_nombre",
    "displayname": {
      "1033": "Numero",
      "3082": "Numero"
    },
    "descripcion": "Número correlativo de la generación, asignado por el sistema.",
    "largo": 20,
    "requerida": false,
    "autonumerico": "GEN-{SEQNUM:8}"
  },
  "columnas": [
    {
      "nombre": "sanic_fechageneracion",
      "displayname": {
        "1033": "Fecha de generacion",
        "3082": "Fecha de generacion"
      },
      "descripcion": "Fecha y hora en que se generó el formato. Se registra de forma explícita porque la bitácora la escribe el sistema, y la fecha de creación del registro no identifica a quien generó.",
      "tipo": "fechahora",
      "requerida": true,
      "protegida": false,
      "auditoria": true
    }
  ]
}
```

### Cuenta de columnas

El diccionario (§4.7) trae **2 columnas propias no-lookup** de la tabla: la primaria autonumérica `sanic_nombre` y `sanic_fechageneracion`. El bloque trae exactamente esas: la primaria en `primaria` y la otra en `columnas[]`. El diccionario trae además **3 lookups**, `sanic_arregloid`, `sanic_versionplantillaid` y `sanic_generadoporid`, que no van aquí (ver sección 8).

### Decisiones que este playbook toma

Solo lo que el diccionario no trae. Nada de esto cambia un nombre lógico, un tipo, un largo, un requerida, una auditoría o una protección.

| # | Decisión | Motivo |
|---|---|---|
| 1 | Las descripciones de la tabla y de la columna son las del diccionario, **acortadas** o reescritas en prosa corrida: se quitan las referencias internas (decisiones D-xx, nombres de campos de sistema) que el usuario no necesita ver en la ayuda de la columna. El dato que describe no cambia | Una descripción es lo que lee el usuario en la plataforma; el detalle de implementación vive en el diccionario |

### Viene del diseño (no es decisión de este playbook)

| Dato | Origen |
|---|---|
| Nombre lógico de la tabla y de la columna, tipo `fechahora` en Usuario local (BP-PP-191, DD-04) | Diccionario §4.7, DD-04 |
| Nombres visibles (singular, plural, primaria, columna), con el mismo texto en 1033 y 3082 | Diccionario §4.7, P-17 y §0; `diseno/01-convenciones.md` §0 |
| `propiedad: usuario` | DD-01 |
| `notas: false`, `actividades: false` | Diccionario §4.7 |
| `auditoria: true` en la tabla y en la columna (D-20: «todas sus columnas se auditan») | Diccionario §4.7, P-15 |
| Primaria `sanic_nombre`: autonumérica con formato `GEN-{SEQNUM:8}` (12 caracteres de ancho; el largo 20 deja margen), `requerida: false` (el diccionario la marca «No»; una autonumérica la asigna el sistema) | Diccionario §4.7, §11.2 (P-02) |
| `sanic_fechageneracion` es `requerida` | Diccionario §4.7 (columna `Req`: «Sí») |
| `motivos_estado: null`: la tabla conserva los dos motivos por defecto | Diccionario §3.2 (solo Conversación y Arreglo tienen motivos propios) |
| Ninguna columna `protegida` | Columna `Seg` del diccionario: «No» en todas las filas |

## 3. Precondiciones

Las de la receta (`power-platform-construir`, `references/modelo-datos/patrones.md` §2.2), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es administrada, y su publisher coincide en unique name, prefijo y prefijo de opciones | los de la sección 1 |
| 2 | Cada idioma de `idiomas` está provisionado, el idioma base del entorno está en la lista y la lista incluye 1033 | `[1033, 3082]` |
| 3 | Ninguna columna de esta tabla usa un choice global | no aplica |
| 4 | Nombres visibles sin tildes ni ñ (BP-PP-197), una entrada por idioma | los de la sección 2 |
| 5 | Todo nombre lógico cumple `sanic_carp_tbl_[a-z0-9]+` (tabla) o `sanic_[a-z0-9]+` (columna), en minúscula y de 95 caracteres o menos | `sanic_carp_tbl_generacionformato` (33 caracteres) |
| 6 | El formato autonumérico de la primaria lleva al menos un marcador y el largo deja margen | `GEN-{SEQNUM:8}` · largo 20 |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/modelo-datos/patrones.md` §2.2 «Tabla con sus columnas», con el contrato de `references/herramientas/patrones.md`. La tabla y su columna viajan en un solo POST (la primaria autonumérica lleva su formato, largo y requerida en el mismo POST). Ninguna columna de este playbook es protegida ni de fórmula: no hay pasos aparte ni pasos manuales.

```
python3 herramientas/construir/tabla.py playbooks/tabla/sanic_carp_tbl_generacionformato.md
```

## 5. Verificación

```
python3 herramientas/construir/tabla.py playbooks/tabla/sanic_carp_tbl_generacionformato.md --solo-verificar
python3 herramientas/construir/muestra_tabla.py playbooks/tabla/sanic_carp_tbl_generacionformato.md --guardar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET EntityDefinitions(LogicalName='sanic_carp_tbl_generacionformato')` | 200 · `OwnershipType = UserOwned` · `HasNotes = false` · `HasActivities = false` · `IsAuditEnabled = true` · `IsManaged = false` · `IsCustomEntity = true` · `PrimaryNameAttribute = sanic_nombre` |
| 2 | Nombre, plural y descripción | Nombre `Generacion de formato` y plural `Generaciones de formato` en 1033 y en 3082, ninguna etiqueta en otro idioma; descripción la de la sección 2 |
| 3 | Primaria `sanic_nombre` | texto autonumérico · `AutoNumberFormat = GEN-{SEQNUM:8}` · largo 20 · no requerida (`None`) · no protegida · `IsAuditEnabled = true` (hereda) · nombre visible `Numero` en 1033 y 3082 · descripción de la sección 2 |
| 4 | `sanic_fechageneracion` | fecha y hora, comportamiento Usuario local · requerida · no protegida · `IsAuditEnabled = true` · `Fecha de generacion` en 1033 y 3082 |
| 5 | Ninguna columna propia de más, salvo lookups | además de las 2 columnas anteriores (primaria incluida), solo pueden existir `sanic_arregloid`, `sanic_versionplantillaid` y `sanic_generadoporid` (lookups, de sus relaciones REL-06, REL-07 y REL-18) |
| 6 | Pertenece a la solución de la sección 1 (tipo de componente 1) | exactamente una fila en `solutioncomponents` |
| 7 | Motivos de estado | como `motivos_estado` es `null`: `statuscode` tiene exactamente el valor 1 (estado 0) y el 2 (estado 1); `statecode` conserva exactamente los estados 0 y 1 |
| 8 | Segunda ejecución de `tabla.py` sin `--solo-verificar` | estado `ya_existia`, código de salida 0, no cambia nada |
| 9 | Muestra real: `muestra_tabla.py … --guardar` (exporta la solución y compara la tabla contra el XML) | XML en `playbooks/tabla/muestras/sanic_carp_tbl_generacionformato.solucion.xml` con los mismos tipos, formato autonumérico, comportamiento de la fecha, requerida, protegida, auditoría y nombres |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5 (1 a 7). Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**.

## 7. Reversa

`DELETE EntityDefinitions(LogicalName='sanic_carp_tbl_generacionformato')`, posible mientras la tabla no tenga datos ni relaciones. **Irreversible desde que se crea**: el nombre lógico de la tabla y el de cada columna; el tipo de propiedad (`usuario`); el tipo de cada columna (la fecha y hora en Usuario local); y el conjunto de estados `statecode`. Se puede cambiar después: nombres visibles, descripciones, requerida, auditoría, y ampliar un largo (achicarlo, no). Borrar la tabla o una columna borra sus datos.

## 8. Fuera de alcance

- Los tres lookups: `sanic_arregloid` (Arreglo, requerido, auditado, REL-06 parental, Cascade All), `sanic_versionplantillaid` (Version de plantilla usada, requerido, auditado, REL-07) y `sanic_generadoporid` (Generado por, `systemuser`, requerido, auditado, REL-18). Nacen con su relación y esta tabla se construye antes que ellas.
- Las reglas que viven en plugins y en la Custom API de generación: que `sanic_generadoporid` y `sanic_fechageneracion` los escriba el sistema (D-26), la purga con el arreglo y el propietario team (P-11). El archivo generado nunca se guarda.
- Vistas, formularios, reglas de negocio, perfil de seguridad de columna (esta tabla no tiene columnas protegidas) y roles.
