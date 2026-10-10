# Playbook: relación · sanic_carp_systemuser_versionplantilla

## 1. Identidad

```json
{
  "tipo_playbook": "relacion",
  "version_skill": "0.2.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "4.17",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09: el publisher `Sistemas_Abiertos_Nicaragua` tiene prefijo `sanic` y prefijo de opciones `15946`, la solución existe sin administrar y `RetrieveProvisionedLanguages` devuelve `[1033, 3082]`. La relación está definida en `diseno/02-diccionario-datos.md` §4.5 (línea 457) y §6.2 (línea 547) y figura en `diseno/06-inventario-componentes.md` §4, renglón 4.17 (REL-17). El número de inventario sigue el orden de construcción: 1 solución, 2 choices globales, 3 tablas, 4 relaciones.

Advertencias: el idioma base del entorno de desarrollo es 1033. Los nombres visibles van en español y sin tildes ni ñ (BP-PP-197), con el mismo texto en los dos idiomas. La descripción es prosa y lleva ortografía completa. Existe otro publisher con el prefijo `sanic` ("Sanic Corp", unique name `sanic`): el playbook fija el publisher por su unique name.

## 2. Qué se crea

```json
{
  "tipo": "relacion",
  "nombre": "sanic_carp_systemuser_versionplantilla",
  "tabla_padre": "systemuser",
  "tabla_hija": "sanic_carp_tbl_versionplantilla",
  "comportamiento": "restringido",
  "lookup": {
    "nombre": "sanic_aprobadoporid",
    "displayname": {
      "1033": "Aprobado por",
      "3082": "Aprobado por"
    },
    "descripcion": "Usuario que aprobó la versión de la plantilla al publicarla. Queda vacío mientras la versión es Borrador.",
    "requerida": false,
    "auditoria": true
  }
}
```

Es una relación referencial con borrado restringido: el padre no se puede borrar mientras tenga hijos. Crea la columna lookup `sanic_aprobadoporid` en `sanic_carp_tbl_versionplantilla`.

Decisiones que este playbook toma y el diseño no traía, o aclara:

| Decisión | Valor | Por qué |
|---|---|---|
| Descripción del lookup | «Usuario que aprobó la versión de la plantilla al publicarla. Queda vacío mientras la versión es Borrador.» | El diccionario trae el nombre lógico, el nombre visible, requerida y auditoría de cada lookup, pero no su descripción. La redacta el playbook a partir de la función del lookup en el diseño; es prosa, con ortografía completa |
| Nombre visible en los dos idiomas | `{"1033": "Aprobado por", "3082": "Aprobado por"}` | El diccionario fija «Aprobado por» (§4.5 (línea 457)); `01-convenciones.md` §0 pide el mismo texto en español en 1033 y 3082, sin tildes ni ñ (BP-PP-197) |

Viene del diseño, no lo decide este playbook:

- Nombre de la relación, padre e hija: `02-diccionario-datos.md` §6.2 (REL-17). Sin sufijo: es el único lookup a `systemuser` de esa tabla (diccionario §6.2, línea 527).
- Nombre lógico del lookup: `sanic_aprobadoporid`, de §4.5 (línea 457).
- Comportamiento `restringido`: `02` §6.2, columna *Comportamiento* (REL-17), con el origen **§6 Datos, P-01**. Una relación con `systemuser` como padre nunca es parental (`relacion.md` §4; `02` §6.1).
- Requerida = no: el *Req* de `sanic_aprobadoporid` en §4.5 (línea 457).
- Auditoría del lookup = sí: columna *Aud* de `sanic_aprobadoporid` (§4.5 (línea 457)); contexto: la tabla hija Versión de plantilla se audita (D-20).

## 3. Precondiciones

Las de la receta (`power-platform-construir`, `references/modelo-datos/patrones.md` §2.3), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es administrada, y su publisher coincide en unique name, prefijo y prefijo de opciones | los de la sección 1 |
| 2 | Cada idioma de `idiomas` está provisionado, y la lista incluye 1033 | `[1033, 3082]` |
| 3 | Las dos tablas existen y la hija es una tabla de la solución | `systemuser` (tabla del sistema) y `sanic_carp_tbl_versionplantilla` (inventario 3.5, construida) |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/modelo-datos/patrones.md` §2.3 "Relación 1:N con su lookup". Herramienta del proyecto: `herramientas/construir/relacion.py`.

```
python3 herramientas/construir/relacion.py playbooks/relacion/sanic_carp_systemuser_versionplantilla.md
```

## 5. Verificación

```
python3 herramientas/construir/relacion.py playbooks/relacion/sanic_carp_systemuser_versionplantilla.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET RelationshipDefinitions(SchemaName='sanic_carp_systemuser_versionplantilla')/Microsoft.Dynamics.CRM.OneToManyRelationshipMetadata` | 200 · `ReferencedEntity = systemuser` · `ReferencedAttribute` = la clave primaria de `systemuser` · `ReferencingEntity = sanic_carp_tbl_versionplantilla` · `ReferencingAttribute = sanic_aprobadoporid` · `IsCustomRelationship = true` · `IsManaged = false` · `IsHierarchical = false` |
| 2 | Cascadas del comportamiento `restringido` | `Assign = NoCascade` · `Merge = NoCascade` · `Reparent = NoCascade` · `Share = NoCascade` · `Unshare = NoCascade` · `RollupView = NoCascade` · `Delete = Restrict` |
| 3 | El lookup `sanic_aprobadoporid` | tipo lookup · `Targets = [systemuser]` · requerida = no · auditoría = sí · sin seguridad de columna · nombre visible «Aprobado por» en 1033 y en 3082 · descripción de la sección 2; ninguna etiqueta en otro idioma |
| 4 | Pertenencia a la solución | la de la tabla hija: `sanic_carp_tbl_versionplantilla` está una vez en la solución de la sección 1, con todos sus subcomponentes (`rootcomponentbehavior = 0`); una relación no es un componente propio |
| 5 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0, no cambia nada |
| 6 | La tabla hija sigue coincidiendo con su propio playbook | `python3 herramientas/construir/tabla.py playbooks/tabla/sanic_carp_tbl_versionplantilla.md --solo-verificar` da `ya_existia`: un lookup no la hace diferir |
| 7 | Muestra real: `python3 herramientas/construir/muestra_relacion.py playbooks/relacion/sanic_carp_systemuser_versionplantilla.md --guardar` (exporta la solución y compara contra `customizations.xml`) | `OK`, y el XML queda en `playbooks/relacion/muestras/` |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**.

## 7. Reversa

`DELETE RelationshipDefinitions(SchemaName='sanic_carp_systemuser_versionplantilla')`. **Borra también la columna lookup `sanic_aprobadoporid` de `sanic_carp_tbl_versionplantilla` y sus datos**, y no es posible mientras el lookup esté en un formulario o en una vista. **Irreversible desde que se crea**: el nombre de la relación y el nombre lógico del lookup. El comportamiento de cascada, el requerida y la auditoría se pueden cambiar después.

## 8. Fuera de alcance

- Claves alternativas: ninguna usa este lookup (K-01 a K-03 usan solo texto, choice y entero; inventario 5.1 a 5.3, `02` §7).
- La escribe el plugin de publicación al pasar la versión a Vigente (`02` §4.5, línea 455).
- Un usuario del sistema no se borra, se deshabilita: el `restringido` en la práctica no se dispara (P-12). El nombre primario de `systemuser` (nombre completo) no es sensible.
- Vistas, formularios y seguridad de registro que muestran este lookup: inventario 12 y 6.
