# Playbook: relación · sanic_carp_conversacion_conversacion

## 1. Identidad

```json
{
  "tipo_playbook": "relacion",
  "version_skill": "0.2.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "4.9",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09: el publisher `Sistemas_Abiertos_Nicaragua` tiene prefijo `sanic` y prefijo de opciones `15946`, la solución existe sin administrar y `RetrieveProvisionedLanguages` devuelve `[1033, 3082]`. La relación está definida en `diseno/02-diccionario-datos.md` §4.2 (línea 344) y §6.2 (línea 539) y figura en `diseno/06-inventario-componentes.md` §4, renglón 4.9 (REL-09). El número de inventario sigue el orden de construcción: 1 solución, 2 choices globales, 3 tablas, 4 relaciones.

Advertencias: el idioma base del entorno de desarrollo es 1033. Los nombres visibles van en español y sin tildes ni ñ (BP-PP-197), con el mismo texto en los dos idiomas. La descripción es prosa y lleva ortografía completa. Existe otro publisher con el prefijo `sanic` ("Sanic Corp", unique name `sanic`): el playbook fija el publisher por su unique name.

**Madurez.** Este playbook es la variante A (relación autorreferida no jerárquica, `relacion.md` §6), con **0 ciclos completos**. Se construye solo, con revisor, y su resultado se anota en el registro de tropiezos de la skill. No se construye en lote.

## 2. Qué se crea

```json
{
  "tipo": "relacion",
  "nombre": "sanic_carp_conversacion_conversacion",
  "tabla_padre": "sanic_carp_tbl_conversacion",
  "tabla_hija": "sanic_carp_tbl_conversacion",
  "jerarquica": false,
  "comportamiento": "quitar_vinculo",
  "lookup": {
    "nombre": "sanic_unidaaid",
    "displayname": {
      "1033": "Unida a",
      "3082": "Unida a"
    },
    "descripcion": "Conversación a la que se unió esta conversación. Solo la fija la unión de conversaciones; queda vacío si nunca se unió.",
    "requerida": false,
    "auditoria": true
  }
}
```

Es una relación con quitar vínculo: al borrar el padre, el lookup de los hijos queda vacío. Crea la columna lookup `sanic_unidaaid` en `sanic_carp_tbl_conversacion`.

Decisiones que este playbook toma y el diseño no traía, o aclara:

| Decisión | Valor | Por qué |
|---|---|---|
| Descripción del lookup | «Conversación a la que se unió esta conversación. Solo la fija la unión de conversaciones; queda vacío si nunca se unió.» | El diccionario trae el nombre lógico, el nombre visible, requerida y auditoría de cada lookup, pero no su descripción. La redacta el playbook a partir de la función del lookup en el diseño; es prosa, con ortografía completa |
| Nombre visible en los dos idiomas | `{"1033": "Unida a", "3082": "Unida a"}` | El diccionario fija «Unida a» (§4.2 (línea 344)); `01-convenciones.md` §0 pide el mismo texto en español en 1033 y 3082, sin tildes ni ñ (BP-PP-197) |

Viene del diseño, no lo decide este playbook:

- Nombre de la relación, padre e hija: `02-diccionario-datos.md` §6.2 (REL-09).
- Nombre lógico del lookup: `sanic_unidaaid`, de §4.2 (línea 344).
- Comportamiento `quitar_vinculo`: `02` §6.2, columna *Comportamiento* (REL-09), con el origen **RF-14, P-12, H-04**.
- Requerida = no: el *Req* de `sanic_unidaaid` en §4.2 (línea 344).
- Auditoría del lookup = sí: columna *Aud* de `sanic_unidaaid` (§4.2 (línea 344)); contexto: la tabla hija Conversación se audita (D-20), con las columnas marcadas en *Aud*.

## 3. Precondiciones

Las de la receta (`power-platform-construir`, `references/modelo-datos/patrones.md` §2.3), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es administrada, y su publisher coincide en unique name, prefijo y prefijo de opciones | los de la sección 1 |
| 2 | Cada idioma de `idiomas` está provisionado, y la lista incluye 1033 | `[1033, 3082]` |
| 3 | La tabla existe y es de la solución | `sanic_carp_tbl_conversacion` (inventario 3.2, construida); es a la vez padre e hija |
| 4 | La relación no es jerárquica y el comportamiento no es parental | `jerarquica = false`; `quitar_vinculo`; lookup no requerido |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/modelo-datos/patrones.md` §2.3 "Relación 1:N con su lookup". Herramienta del proyecto: `herramientas/construir/relacion.py`.

```
python3 herramientas/construir/relacion.py playbooks/relacion/sanic_carp_conversacion_conversacion.md
```

**Estado esperado de la herramienta: `bloqueado`.** La variante A (autorreferida no jerárquica) está implementada pero no verificada contra la plataforma; `relacion.py` devuelve `bloqueado` hasta que un ensayo sobre una tabla descartable la dé por verificada. Este playbook no esquiva ese bloqueo ni cambia su contenido para pasarlo. Lo sin verificar: que el alta con `IsHierarchical: false` explícito sea aceptada y devuelta así; el error exacto de la plataforma online con comportamiento parental en una autorreferida (la prohibición del formato no depende de eso); y si la plataforma impide que un registro se apunte a sí mismo o forme ciclos.

## 5. Verificación

```
python3 herramientas/construir/relacion.py playbooks/relacion/sanic_carp_conversacion_conversacion.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET RelationshipDefinitions(SchemaName='sanic_carp_conversacion_conversacion')/Microsoft.Dynamics.CRM.OneToManyRelationshipMetadata` | 200 · `ReferencedEntity = sanic_carp_tbl_conversacion` · `ReferencedAttribute` = la clave primaria de `sanic_carp_tbl_conversacion` · `ReferencingEntity = sanic_carp_tbl_conversacion` · `ReferencingAttribute = sanic_unidaaid` · `IsCustomRelationship = true` · `IsManaged = false` · `IsHierarchical = false` · `ReferencedEntity` igual a `ReferencingEntity` |
| 2 | Cascadas del comportamiento `quitar_vinculo` | `Assign = NoCascade` · `Merge = NoCascade` · `Reparent = NoCascade` · `Share = NoCascade` · `Unshare = NoCascade` · `RollupView = NoCascade` · `Delete = RemoveLink` |
| 3 | El lookup `sanic_unidaaid` | tipo lookup · `Targets = [sanic_carp_tbl_conversacion]` · requerida = no · auditoría = sí · sin seguridad de columna · nombre visible «Unida a» en 1033 y en 3082 · descripción de la sección 2; ninguna etiqueta en otro idioma |
| 4 | Es autorreferida y no jerárquica | `IsHierarchical = false`; `ReferencedEntity` = `ReferencingEntity` = `sanic_carp_tbl_conversacion`; `Targets` del lookup = `[sanic_carp_tbl_conversacion]` |
| 5 | Pertenencia a la solución | la de la tabla hija: `sanic_carp_tbl_conversacion` está una vez en la solución de la sección 1, con todos sus subcomponentes (`rootcomponentbehavior = 0`); una relación no es un componente propio (la tabla es padre e hija a la vez) |
| 6 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0, no cambia nada |
| 7 | La tabla hija sigue coincidiendo con su propio playbook | `python3 herramientas/construir/tabla.py playbooks/tabla/sanic_carp_tbl_conversacion.md --solo-verificar` da `ya_existia`: un lookup no la hace diferir |
| 8 | Muestra real: `python3 herramientas/construir/muestra_relacion.py playbooks/relacion/sanic_carp_conversacion_conversacion.md --guardar` (exporta la solución y compara contra `customizations.xml`) | `OK`, y el XML queda en `playbooks/relacion/muestras/` |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**.

## 7. Reversa

`DELETE RelationshipDefinitions(SchemaName='sanic_carp_conversacion_conversacion')`. **Borra también la columna lookup `sanic_unidaaid` de `sanic_carp_tbl_conversacion` y sus datos**, y no es posible mientras el lookup esté en un formulario o en una vista. **Irreversible desde que se crea**: el nombre de la relación y el nombre lógico del lookup. El comportamiento de cascada, el requerida y la auditoría se pueden cambiar después.

## 8. Fuera de alcance

- Claves alternativas: ninguna usa este lookup (K-01 a K-03 usan solo texto, choice y entero; inventario 5.1 a 5.3, `02` §7).
- **Ciclos y autorreferencia.** Learn no documenta que la plataforma impida que una conversación se una a sí misma ni que se formen ciclos, y el comportamiento está pendiente de verificar en una tabla descartable. Si el negocio los prohíbe, es una regla de la Custom API «Unir conversaciones» o de un plugin, y se especifica en su propio playbook (inventario 7, 8).
- El estado «Unida» (`statuscode` 159460001) lo fija solo la Custom API de unir (D-30, `02` §3.2); este playbook no lo toca.
- Esta relación no es la jerarquía de la tabla: no se marca `IsHierarchical` (decisión sobre toda la tabla, otro tipo de playbook). No ocupa el lugar de la relación parental que admite la tabla hija, porque no tiene cascadas.
- Vistas, formularios y seguridad de registro que muestran este lookup: inventario 12 y 6.
