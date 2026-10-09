# Playbook: choice-global · sanic_carp_ch_campomapeable

## 1. Identidad

```json
{
  "tipo_playbook": "choice-global",
  "version_skill": "0.2.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "2.9",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09: el publisher `Sistemas_Abiertos_Nicaragua` tiene prefijo `sanic` y prefijo de opciones `15946`, la solución existe sin administrar y `RetrieveProvisionedLanguages` devuelve `[1033, 3082]`. El componente está definido en `diseno/02-diccionario-datos.md` §3.1. El número de inventario sigue el orden de construcción: 1 solución, 2 choices globales, 3 tablas.

Advertencias: el idioma base del entorno de desarrollo es 1033. Las etiquetas son las mismas, en español y sin tildes (BP-PP-197), en los dos idiomas. La descripción va en prosa con tildes: no es un nombre visible.

## 2. Qué se crea

```json
{
  "tipo": "choice-global",
  "nombre": "sanic_carp_ch_campomapeable",
  "displayname": {
    "1033": "CH - CARP - Campo mapeable",
    "3082": "CH - CARP - Campo mapeable"
  },
  "descripcion": "Campos que el generador del formato sabe escribir en una celda. La lista está acoplada al código del generador y la mantiene el equipo de la solución, no el negocio.",
  "opciones": [
    {
      "valor": 159460001,
      "etiqueta": {
        "1033": "Fecha de formalizacion",
        "3082": "Fecha de formalizacion"
      },
      "descripcion": "Clave del generador: fechaFormalizacion. Tipo de mapeo que admite: Valor simple"
    },
    {
      "valor": 159460002,
      "etiqueta": {
        "1033": "Oficial de arreglo",
        "3082": "Oficial de arreglo"
      },
      "descripcion": "Clave del generador: oficial. Tipo de mapeo que admite: Valor simple"
    },
    {
      "valor": 159460003,
      "etiqueta": {
        "1033": "Nombre del cliente",
        "3082": "Nombre del cliente"
      },
      "descripcion": "Clave del generador: clienteNombre. Tipo de mapeo que admite: Valor simple"
    },
    {
      "valor": 159460004,
      "etiqueta": {
        "1033": "Cedula del cliente",
        "3082": "Cedula del cliente"
      },
      "descripcion": "Clave del generador: clienteCedula. Tipo de mapeo que admite: Valor simple"
    },
    {
      "valor": 159460005,
      "etiqueta": {
        "1033": "Correo del cliente",
        "3082": "Correo del cliente"
      },
      "descripcion": "Clave del generador: clienteCorreo. Tipo de mapeo que admite: Valor simple"
    },
    {
      "valor": 159460006,
      "etiqueta": {
        "1033": "Celular del cliente",
        "3082": "Celular del cliente"
      },
      "descripcion": "Clave del generador: clienteCelular. Tipo de mapeo que admite: Valor simple"
    },
    {
      "valor": 159460007,
      "etiqueta": {
        "1033": "Numero de prestamo o tarjeta",
        "3082": "Numero de prestamo o tarjeta"
      },
      "descripcion": "Clave del generador: numeroProducto. Tipo de mapeo que admite: Valor simple"
    },
    {
      "valor": 159460008,
      "etiqueta": {
        "1033": "Tipo de arreglo",
        "3082": "Tipo de arreglo"
      },
      "descripcion": "Clave del generador: tipo. Tipo de mapeo que admite: Valor simple"
    },
    {
      "valor": 159460009,
      "etiqueta": {
        "1033": "Numero de arreglo",
        "3082": "Numero de arreglo"
      },
      "descripcion": "Clave del generador: numeroArreglo. Tipo de mapeo que admite: Valor simple"
    },
    {
      "valor": 159460010,
      "etiqueta": {
        "1033": "Saldo inicial",
        "3082": "Saldo inicial"
      },
      "descripcion": "Clave del generador: saldoInicial. Tipo de mapeo que admite: Valor simple"
    },
    {
      "valor": 159460011,
      "etiqueta": {
        "1033": "Monto abonado",
        "3082": "Monto abonado"
      },
      "descripcion": "Clave del generador: montoAbonado. Tipo de mapeo que admite: Valor simple"
    },
    {
      "valor": 159460012,
      "etiqueta": {
        "1033": "Saldo remanente",
        "3082": "Saldo remanente"
      },
      "descripcion": "Clave del generador: saldoRemanente. Tipo de mapeo que admite: Valor simple"
    },
    {
      "valor": 159460013,
      "etiqueta": {
        "1033": "Plazo en cuotas",
        "3082": "Plazo en cuotas"
      },
      "descripcion": "Clave del generador: plazo. Tipo de mapeo que admite: Valor simple"
    },
    {
      "valor": 159460014,
      "etiqueta": {
        "1033": "Cuota mensual",
        "3082": "Cuota mensual"
      },
      "descripcion": "Clave del generador: cuota. Tipo de mapeo que admite: Valor simple"
    },
    {
      "valor": 159460015,
      "etiqueta": {
        "1033": "Total a pagar",
        "3082": "Total a pagar"
      },
      "descripcion": "Clave del generador: totalPagar. Tipo de mapeo que admite: Valor simple"
    },
    {
      "valor": 159460016,
      "etiqueta": {
        "1033": "Fecha de inicio",
        "3082": "Fecha de inicio"
      },
      "descripcion": "Clave del generador: fechaInicio. Tipo de mapeo que admite: Valor simple"
    },
    {
      "valor": 159460017,
      "etiqueta": {
        "1033": "Fecha de finalizacion",
        "3082": "Fecha de finalizacion"
      },
      "descripcion": "Clave del generador: fechaFin. Tipo de mapeo que admite: Valor simple"
    },
    {
      "valor": 159460018,
      "etiqueta": {
        "1033": "Cuenta a debitar",
        "3082": "Cuenta a debitar"
      },
      "descripcion": "Clave del generador: cuentaDebitar. Tipo de mapeo que admite: Valor simple"
    },
    {
      "valor": 159460019,
      "etiqueta": {
        "1033": "Comentarios",
        "3082": "Comentarios"
      },
      "descripcion": "Clave del generador: comentarios. Tipo de mapeo que admite: Valor simple"
    },
    {
      "valor": 159460020,
      "etiqueta": {
        "1033": "Cuota: numero",
        "3082": "Cuota: numero"
      },
      "descripcion": "Clave del generador: cuota.numero. Tipo de mapeo que admite: Columna de bloque de cuotas"
    },
    {
      "valor": 159460021,
      "etiqueta": {
        "1033": "Cuota: fecha de vencimiento",
        "3082": "Cuota: fecha de vencimiento"
      },
      "descripcion": "Clave del generador: cuota.fecha. Tipo de mapeo que admite: Columna de bloque de cuotas"
    },
    {
      "valor": 159460022,
      "etiqueta": {
        "1033": "Cuota: monto",
        "3082": "Cuota: monto"
      },
      "descripcion": "Clave del generador: cuota.monto. Tipo de mapeo que admite: Columna de bloque de cuotas"
    },
    {
      "valor": 159460023,
      "etiqueta": {
        "1033": "Cuota: fecha de pago",
        "3082": "Cuota: fecha de pago"
      },
      "descripcion": "Clave del generador: cuota.fechaPago. Tipo de mapeo que admite: Columna de bloque de cuotas"
    },
    {
      "valor": 159460024,
      "etiqueta": {
        "1033": "Cuota: monto pagado",
        "3082": "Cuota: monto pagado"
      },
      "descripcion": "Clave del generador: cuota.montoPagado. Tipo de mapeo que admite: Columna de bloque de cuotas"
    },
    {
      "valor": 159460025,
      "etiqueta": {
        "1033": "Total: montos de cuota",
        "3082": "Total: montos de cuota"
      },
      "descripcion": "Clave del generador: total.montoCuotas. Tipo de mapeo que admite: Total calculado"
    },
    {
      "valor": 159460026,
      "etiqueta": {
        "1033": "Total: montos pagados",
        "3082": "Total: montos pagados"
      },
      "descripcion": "Clave del generador: total.montoPagado. Tipo de mapeo que admite: Total calculado"
    },
    {
      "valor": 159460027,
      "etiqueta": {
        "1033": "Moneda",
        "3082": "Moneda"
      },
      "descripcion": "Clave del generador: moneda. Tipo de mapeo que admite: Valor simple"
    }
  ]
}
```

## 3. Precondiciones

Las de la receta (`power-platform-construir`, `references/modelo-datos/patrones.md` §2.1), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es administrada, y su publisher coincide en unique name, prefijo y prefijo de opciones | los de la sección 1 |
| 2 | Todos los valores están en el rango del publisher | `159460000 ≤ valor < 159470000` |
| 3 | Cada idioma de `idiomas` está provisionado, y la lista incluye 1033 | `[1033, 3082]` |
| 4 | Nombres visibles sin tildes ni ñ (BP-PP-197) y display name con el patrón `CH - CARP - Nombre` | los de la sección 2 |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/modelo-datos/patrones.md` §2.1 "Choice global", con el contrato de `references/herramientas/patrones.md`.

```
python3 herramientas/construir/choice_global.py playbooks/choice-global/sanic_carp_ch_campomapeable.md
```

## 5. Verificación

```
python3 herramientas/construir/choice_global.py playbooks/choice-global/sanic_carp_ch_campomapeable.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET GlobalOptionSetDefinitions(Name='sanic_carp_ch_campomapeable')` | 200 · `IsGlobal = true` · `OptionSetType = Picklist` · `IsManaged = false` |
| 2 | Display name en 1033 y en 3082, y descripción | los de la sección 2, ninguna etiqueta en otro idioma |
| 3 | Opciones, en orden, con valor, etiqueta por idioma y descripción | `159460001 = Fecha de formalizacion`, `159460002 = Oficial de arreglo`, `159460003 = Nombre del cliente`, `159460004 = Cedula del cliente`, `159460005 = Correo del cliente`, `159460006 = Celular del cliente`, `159460007 = Numero de prestamo o tarjeta`, `159460008 = Tipo de arreglo`, `159460009 = Numero de arreglo`, `159460010 = Saldo inicial`, `159460011 = Monto abonado`, `159460012 = Saldo remanente`, `159460013 = Plazo en cuotas`, `159460014 = Cuota mensual`, `159460015 = Total a pagar`, `159460016 = Fecha de inicio`, `159460017 = Fecha de finalizacion`, `159460018 = Cuenta a debitar`, `159460019 = Comentarios`, `159460020 = Cuota: numero`, `159460021 = Cuota: fecha de vencimiento`, `159460022 = Cuota: monto`, `159460023 = Cuota: fecha de pago`, `159460024 = Cuota: monto pagado`, `159460025 = Total: montos de cuota`, `159460026 = Total: montos pagados`, `159460027 = Moneda`; ninguna más |
| 4 | Pertenece a la solución de la sección 1 | exactamente una fila en `solutioncomponents` |
| 5 | Segunda ejecución de la herramienta sin `--solo-verificar` | estado `ya_existia`, código de salida 0, no cambia nada |
| 6 | Muestra real: `python3 herramientas/construir/muestra_choice.py --guardar` (exporta la solución y compara todos los choices) | XML exportado en `playbooks/choice-global/muestras/sanic_carp_ch_campomapeable.solucion.xml`, con las mismas opciones y etiquetas |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**.

## 7. Reversa

`DELETE GlobalOptionSetDefinitions(Name='sanic_carp_ch_campomapeable')`, posible solo mientras ninguna columna lo use. **Irreversible desde que tenga uso**: el nombre lógico `sanic_carp_ch_campomapeable`, y los valores `159460001` a `159460027` una vez que haya datos con ellos; nunca se renumeran ni se reutilizan (BP-PP-193). Una opción que deja de usarse se retira de las vistas, no se borra.

## 8. Fuera de alcance

La columna `sanic_campo` de Mapeo de celda que usa este choice: nace con el playbook de su tabla (inventario 3.6). El código C# no usa los literales numéricos: usa el `enum` que genera `herramientas/codigo/generar_enums.py` desde este playbook. No hay opciones reservadas para fases posteriores.
