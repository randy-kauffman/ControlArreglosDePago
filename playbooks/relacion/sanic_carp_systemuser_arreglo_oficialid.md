# Playbook: relación · sanic_carp_systemuser_arreglo_oficialid

## 1. Identidad

```json
{
  "tipo_playbook": "relacion",
  "version_skill": "0.2.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "4.12",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09: el publisher `Sistemas_Abiertos_Nicaragua` tiene prefijo `sanic` y prefijo de opciones `15946`, la solución existe sin administrar y `RetrieveProvisionedLanguages` devuelve `[1033, 3082]`. La relación está definida en `diseno/02-diccionario-datos.md` §4.3 (línea 405) y §6.2 (línea 542) y figura en `diseno/06-inventario-componentes.md` §4, renglón 4.12 (REL-12). El número de inventario sigue el orden de construcción: 1 solución, 2 choices globales, 3 tablas, 4 relaciones.

Advertencias: el idioma base del entorno de desarrollo es 1033. Los nombres visibles van en español y sin tildes ni ñ (BP-PP-197), con el mismo texto en los dos idiomas. La descripción es prosa y lleva ortografía completa. Existe otro publisher con el prefijo `sanic` ("Sanic Corp", unique name `sanic`): el playbook fija el publisher por su unique name.

## 2. Qué se crea

```json
{
  "tipo": "relacion",
  "nombre": "sanic_carp_systemuser_arreglo_oficialid",
  "tabla_padre": "systemuser",
  "tabla_hija": "sanic_carp_tbl_arreglo",
  "comportamiento": "restringido",
  "lookup": {
    "nombre": "sanic_oficialid",
    "displayname": {
      "1033": "Oficial de arreglo",
      "3082": "Oficial de arreglo"
    },
    "descripcion": "Oficial responsable del arreglo.",
    "requerida": true,
    "auditoria": false
  }
}
```

Es una relación referencial con borrado restringido: el padre no se puede borrar mientras tenga hijos. Crea la columna lookup `sanic_oficialid` en `sanic_carp_tbl_arreglo`.

Decisiones que este playbook toma y el diseño no traía, o aclara:

| Decisión | Valor | Por qué |
|---|---|---|
| Descripción del lookup | «Oficial responsable del arreglo.» | El diccionario trae el nombre lógico, el nombre visible, requerida y auditoría de cada lookup, pero no su descripción. La redacta el playbook a partir de la función del lookup en el diseño; es prosa, con ortografía completa |
| Nombre visible en los dos idiomas | `{"1033": "Oficial de arreglo", "3082": "Oficial de arreglo"}` | El diccionario fija «Oficial de arreglo» (§4.3 (línea 405)); `01-convenciones.md` §0 pide el mismo texto en español en 1033 y 3082, sin tildes ni ñ (BP-PP-197) |

Viene del diseño, no lo decide este playbook:

- Nombre de la relación, padre e hija: `02-diccionario-datos.md` §6.2 (REL-12). Lleva el sufijo `_oficialid` porque hay más de una relación entre `systemuser` y `sanic_carp_tbl_arreglo`: BP-PP-188 y P-01 (el sufijo es el nombre completo del lookup sin prefijo).
- Nombre lógico del lookup: `sanic_oficialid`, de §4.3 (línea 405).
- Comportamiento `restringido`: `02` §6.2, columna *Comportamiento* (REL-12), con el origen **RF-06, P-01**. Una relación con `systemuser` como padre nunca es parental (`relacion.md` §4; `02` §6.1).
- Requerida = sí: el *Req* de `sanic_oficialid` en §4.3 (línea 405).
- Auditoría del lookup = no: columna *Aud* de `sanic_oficialid` (§4.3 (línea 405)); contexto: la tabla hija Arreglo se audita solo por las columnas marcadas en *Aud* (`02` §4.3, línea 356).

## 3. Precondiciones

Las de la receta (`power-platform-construir`, `references/modelo-datos/patrones.md` §2.3), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es administrada, y su publisher coincide en unique name, prefijo y prefijo de opciones | los de la sección 1 |
| 2 | Cada idioma de `idiomas` está provisionado, y la lista incluye 1033 | `[1033, 3082]` |
| 3 | Las dos tablas existen y la hija es una tabla de la solución | `systemuser` (tabla del sistema) y `sanic_carp_tbl_arreglo` (inventario 3.3, construida) |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/modelo-datos/patrones.md` §2.3 "Relación 1:N con su lookup". Herramienta del proyecto: `herramientas/construir/relacion.py`.

```
python3 herramientas/construir/relacion.py playbooks/relacion/sanic_carp_systemuser_arreglo_oficialid.md
```

## 5. Verificación

```
python3 herramientas/construir/relacion.py playbooks/relacion/sanic_carp_systemuser_arreglo_oficialid.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET RelationshipDefinitions(SchemaName='sanic_carp_systemuser_arreglo_oficialid')/Microsoft.Dynamics.CRM.OneToManyRelationshipMetadata` | 200 · `ReferencedEntity = systemuser` · `ReferencedAttribute` = la clave primaria de `systemuser` · `ReferencingEntity = sanic_carp_tbl_arreglo` · `ReferencingAttribute = sanic_oficialid` · `IsCustomRelationship = true` · `IsManaged = false` · `IsHierarchical = false` |
| 2 | Cascadas del comportamiento `restringido` | `Assign = NoCascade` · `Merge = NoCascade` · `Reparent = NoCascade` · `Share = NoCascade` · `Unshare = NoCascade` · `RollupView = NoCascade` · `Delete = Restrict` |
| 3 | El lookup `sanic_oficialid` | tipo lookup · `Targets = [systemuser]` · requerida = sí · auditoría = no · sin seguridad de columna · nombre visible «Oficial de arreglo» en 1033 y en 3082 · descripción de la sección 2; ninguna etiqueta en otro idioma |
| 4 | Pertenencia a la solución | la de la tabla hija: `sanic_carp_tbl_arreglo` está una vez en la solución de la sección 1, con todos sus subcomponentes (`rootcomponentbehavior = 0`); una relación no es un componente propio |
| 5 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0, no cambia nada |
| 6 | La tabla hija sigue coincidiendo con su propio playbook | `python3 herramientas/construir/tabla.py playbooks/tabla/sanic_carp_tbl_arreglo.md --solo-verificar` da `ya_existia`: un lookup no la hace diferir |
| 7 | Muestra real: `python3 herramientas/construir/muestra_relacion.py playbooks/relacion/sanic_carp_systemuser_arreglo_oficialid.md --guardar` (exporta la solución y compara contra `customizations.xml`) | `OK`, y el XML queda en `playbooks/relacion/muestras/` |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**.

## 7. Reversa

`DELETE RelationshipDefinitions(SchemaName='sanic_carp_systemuser_arreglo_oficialid')`. **Borra también la columna lookup `sanic_oficialid` de `sanic_carp_tbl_arreglo` y sus datos**, y no es posible mientras el lookup esté en un formulario o en una vista. **Irreversible desde que se crea**: el nombre de la relación y el nombre lógico del lookup. El comportamiento de cascada, el requerida y la auditoría se pueden cambiar después.

## 8. Fuera de alcance

- Claves alternativas: ninguna usa este lookup (K-01 a K-03 usan solo texto, choice y entero; inventario 5.1 a 5.3, `02` §7).
- Quién se elige como oficial al registrar el arreglo: formulario y plugin pre-create, no esta relación.
- Un usuario del sistema no se borra, se deshabilita: el `restringido` en la práctica no se dispara (P-12). El nombre primario de `systemuser` (nombre completo) no es sensible.
- Vistas, formularios y seguridad de registro que muestran este lookup: inventario 12 y 6.
