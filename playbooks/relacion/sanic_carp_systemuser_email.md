# Playbook: relación · sanic_carp_systemuser_email

## 1. Identidad

```json
{
  "tipo_playbook": "relacion",
  "version_skill": "0.2.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "4.19",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09: el publisher `Sistemas_Abiertos_Nicaragua` tiene prefijo `sanic` y prefijo de opciones `15946`, la solución existe sin administrar y `RetrieveProvisionedLanguages` devuelve `[1033, 3082]`. La relación está definida en `diseno/02-diccionario-datos.md` §5 (línea 508) y §6.2 (línea 549) y figura en `diseno/06-inventario-componentes.md` §4, renglón 4.19 (REL-19). El número de inventario sigue el orden de construcción: 1 solución, 2 choices globales, 3 tablas, 4 relaciones.

Advertencias: el idioma base del entorno de desarrollo es 1033. Los nombres visibles van en español y sin tildes ni ñ (BP-PP-197), con el mismo texto en los dos idiomas. La descripción es prosa y lleva ortografía completa. Existe otro publisher con el prefijo `sanic` ("Sanic Corp", unique name `sanic`): el playbook fija el publisher por su unique name.

**Madurez.** Este playbook es la variante B (relación cuya tabla hija es una tabla estándar, `relacion.md` §6), con **0 ciclos completos**. Se construye solo, con revisor, y su resultado se anota en el registro de tropiezos de la skill. No se construye en lote.

## 2. Qué se crea

```json
{
  "tipo": "relacion",
  "nombre": "sanic_carp_systemuser_email",
  "tabla_padre": "systemuser",
  "tabla_hija": "email",
  "comportamiento": "restringido",
  "lookup": {
    "nombre": "sanic_respondidoporid",
    "displayname": {
      "1033": "Respondido por",
      "3082": "Respondido por"
    },
    "descripcion": "Oficial que envió la respuesta. Lo escribe el plugin y permite agrupar el promedio de tiempo de respuesta por oficial.",
    "requerida": false,
    "auditoria": false
  }
}
```

Es una relación referencial con borrado restringido: el padre no se puede borrar mientras tenga hijos. Crea la columna lookup `sanic_respondidoporid` en la tabla estándar `email`, que ya existe en la plataforma: este playbook no crea la tabla ni la modifica más que con ese lookup.

Decisiones que este playbook toma y el diseño no traía, o aclara:

| Decisión | Valor | Por qué |
|---|---|---|
| Descripción del lookup | «Oficial que envió la respuesta. Lo escribe el plugin y permite agrupar el promedio de tiempo de respuesta por oficial.» | El diccionario trae el nombre lógico, el nombre visible, requerida y auditoría de cada lookup, pero no su descripción. La redacta el playbook a partir de la función del lookup en el diseño; es prosa, con ortografía completa |
| Nombre visible en los dos idiomas | `{"1033": "Respondido por", "3082": "Respondido por"}` | El diccionario fija «Respondido por» (§5 (línea 508)); `01-convenciones.md` §0 pide el mismo texto en español en 1033 y 3082, sin tildes ni ñ (BP-PP-197) |

Viene del diseño, no lo decide este playbook:

- Nombre de la relación, padre e hija: `02-diccionario-datos.md` §6.2 (REL-19). Sin sufijo: es el único lookup a `systemuser` de esa tabla (diccionario §6.2, línea 527).
- Nombre lógico del lookup: `sanic_respondidoporid`, de §5 (línea 508).
- Comportamiento `restringido`: `02` §6.2, columna *Comportamiento* (REL-19), con el origen **RF-03, RF-04, D-05, H-02**. Una relación con `systemuser` como padre nunca es parental (`relacion.md` §4; `02` §6.1).
- Requerida = no: el *Req* de `sanic_respondidoporid` en §5 (línea 508).
- Auditoría del lookup = no: columna *Aud* de `sanic_respondidoporid` (§5 (línea 508)); contexto: `02` §5 no cambia la auditoría de `email` (línea 503) y marca el lookup *Aud* = No.

## 3. Precondiciones

Las de la receta (`power-platform-construir`, `references/modelo-datos/patrones.md` §2.3), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es administrada, y su publisher coincide en unique name, prefijo y prefijo de opciones | los de la sección 1 |
| 2 | Cada idioma de `idiomas` está provisionado, y la lista incluye 1033 | `[1033, 3082]` |
| 3 | La tabla padre existe | `systemuser` (tabla del sistema) |
| 4 | La tabla hija estándar existe, tiene las columnas propias del playbook 3.8 y puede ser la referenciante | `email` (`IsCustomEntity = false`; inventario 3.8 construido); `CanBeReferencing(email)` y `CanBeReferenced(systemuser)` = `true` (verificado el 2026-10-09) |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/modelo-datos/patrones.md` §2.3 "Relación 1:N con su lookup". Herramienta del proyecto: `herramientas/construir/relacion.py`.

```
python3 herramientas/construir/relacion.py playbooks/relacion/sanic_carp_systemuser_email.md
```

**Construida el 2026-10-09.** La variante B (hija estándar) se ensayó ese día con un lookup descartable sobre `email` y quedó implementada y verificada en `relacion.py`: comprueba `CanBeReferencing`/`CanBeReferenced` antes de crear y exige la pertenencia observada (relación y lookup con una fila cada uno, `email` una vez con `rootcomponentbehavior` 1). Resultado: `creado`, después `ya_existia`, y la muestra exportada coincide.

## 5. Verificación

```
python3 herramientas/construir/relacion.py playbooks/relacion/sanic_carp_systemuser_email.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET RelationshipDefinitions(SchemaName='sanic_carp_systemuser_email')/Microsoft.Dynamics.CRM.OneToManyRelationshipMetadata` | 200 · `ReferencedEntity = systemuser` · `ReferencedAttribute` = la clave primaria de `systemuser` · `ReferencingEntity = email` · `ReferencingAttribute = sanic_respondidoporid` · `IsCustomRelationship = true` · `IsManaged = false` · `IsHierarchical = false` |
| 2 | Cascadas del comportamiento `restringido` | `Assign = NoCascade` · `Merge = NoCascade` · `Reparent = NoCascade` · `Share = NoCascade` · `Unshare = NoCascade` · `RollupView = NoCascade` · `Delete = Restrict` |
| 3 | El lookup `sanic_respondidoporid` | tipo lookup · `Targets = [systemuser]` · requerida = no · auditoría = no · sin seguridad de columna · nombre visible «Respondido por» en 1033 y en 3082 · descripción de la sección 2; ninguna etiqueta en otro idioma |
| 4 | Pertenencia a la solución (variante B) | La relación con 1 fila (`componenttype` 10), el lookup con 1 fila (`componenttype` 2) y `email` exactamente 1 vez con `rootcomponentbehavior` distinto de 0 (observado: 1). Criterio fijado por el ensayo del 2026-10-09 |
| 5 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0, no cambia nada |
| 6 | La tabla estándar sigue coincidiendo con su playbook de columnas | `python3 herramientas/construir/columnas_estandar.py playbooks/tabla/email.md --solo-verificar` da `ya_existia`: el lookup no la hace diferir. No se compara contra un playbook de tabla propia |
| 7 | Muestra real: `python3 herramientas/construir/muestra_relacion.py playbooks/relacion/sanic_carp_systemuser_email.md --guardar` | La `EntityRelationship` coincide (`Email` y `SystemUser` comparados sin distinguir mayúsculas) y `solution.xml` trae `email` como `RootComponent` una vez con `behavior` distinto de 0 |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**.

## 7. Reversa

`DELETE RelationshipDefinitions(SchemaName='sanic_carp_systemuser_email')`. **Borra también la columna lookup `sanic_respondidoporid` de `email` y sus datos**, y no es posible mientras el lookup esté en un formulario o en una vista. **Irreversible desde que se crea**: el nombre de la relación y el nombre lógico del lookup. El comportamiento de cascada, el requerida y la auditoría se pueden cambiar después.

## 8. Fuera de alcance

- Claves alternativas: ninguna usa este lookup (K-01 a K-03 usan solo texto, choice y entero; inventario 5.1 a 5.3, `02` §7).
- Quién escribe la columna (el plugin al pasar el Email a Enviado) y la columna hermana `sanic_tiemporespuesta`: playbook 3.8 (`playbooks/tabla/email.md`).
- El gráfico `CHT - CARP - Promedio por respuesta` (inventario 12.15) agrupa por este lookup.
- La relación nativa Regarding Email → Conversación (NAT-01) no se crea ni se toca aquí: la crea la plataforma al activar actividades en Conversación. `email` ya es hija en esa relación con cascadas, y por eso esta relación no puede ser parental.
- Inconsistencia anotada en el inventario (punto 11): `02` línea 508 y 776 ponen el lookup junto a `sanic_tiemporespuesta`; el lookup nace aquí y no en 3.8.
- Vistas, formularios y seguridad de registro que muestran este lookup: inventario 12 y 6.
