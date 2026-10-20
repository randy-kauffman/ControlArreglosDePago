# Playbook: datos · Mapeo de celda de v07

> Las 28 filas de `MAP_V07` (`mockup/index.html` 262-271), medidas en la tanda 1 de humo del 2026-10-20 (H-23): 19 valores simples de la hoja del formato, 2 de la hoja de control, 5 columnas del bloque de cuotas y 2 totales. Recarga controlada (OA-4).

## 1. Identidad

```json
{
  "tipo_playbook": "datos",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "9.2",
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

La tabla está en `diseno/02-diccionario-datos.md` §4.6; las etiquetas de `sanic_campo` en §3.1 (`sanic_carp_ch_campomapeable`).

## 2. Qué se crea

```json
{
  "tipo": "datos",
  "tabla": "sanic_carp_tbl_mapeocelda",
  "clave": [
    "sanic_versionplantillaid",
    "sanic_hoja",
    "sanic_celda"
  ],
  "filas": [
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": " FORMATO ARP ELECTRONICO",
      "sanic_celda": "B7",
      "sanic_nombre": " FORMATO ARP ELECTRONICO!B7",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Valor simple"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Fecha de formalizacion"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      }
    },
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": " FORMATO ARP ELECTRONICO",
      "sanic_celda": "F7",
      "sanic_nombre": " FORMATO ARP ELECTRONICO!F7",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Valor simple"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Oficial de arreglo"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      }
    },
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": " FORMATO ARP ELECTRONICO",
      "sanic_celda": "B8",
      "sanic_nombre": " FORMATO ARP ELECTRONICO!B8",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Valor simple"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Nombre del cliente"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      }
    },
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": " FORMATO ARP ELECTRONICO",
      "sanic_celda": "F8",
      "sanic_nombre": " FORMATO ARP ELECTRONICO!F8",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Valor simple"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Cedula del cliente"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      }
    },
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": " FORMATO ARP ELECTRONICO",
      "sanic_celda": "B9",
      "sanic_nombre": " FORMATO ARP ELECTRONICO!B9",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Valor simple"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Correo del cliente"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      }
    },
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": " FORMATO ARP ELECTRONICO",
      "sanic_celda": "F9",
      "sanic_nombre": " FORMATO ARP ELECTRONICO!F9",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Valor simple"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Celular del cliente"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      }
    },
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": " FORMATO ARP ELECTRONICO",
      "sanic_celda": "B12",
      "sanic_nombre": " FORMATO ARP ELECTRONICO!B12",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Valor simple"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Numero de prestamo o tarjeta"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      }
    },
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": " FORMATO ARP ELECTRONICO",
      "sanic_celda": "D12",
      "sanic_nombre": " FORMATO ARP ELECTRONICO!D12",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Valor simple"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Tipo de arreglo"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      }
    },
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": " FORMATO ARP ELECTRONICO",
      "sanic_celda": "F12",
      "sanic_nombre": " FORMATO ARP ELECTRONICO!F12",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Valor simple"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Numero de arreglo"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      }
    },
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": " FORMATO ARP ELECTRONICO",
      "sanic_celda": "B13",
      "sanic_nombre": " FORMATO ARP ELECTRONICO!B13",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Valor simple"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Saldo inicial"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      }
    },
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": " FORMATO ARP ELECTRONICO",
      "sanic_celda": "D13",
      "sanic_nombre": " FORMATO ARP ELECTRONICO!D13",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Valor simple"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Monto abonado"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      }
    },
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": " FORMATO ARP ELECTRONICO",
      "sanic_celda": "F13",
      "sanic_nombre": " FORMATO ARP ELECTRONICO!F13",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Valor simple"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Saldo remanente"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      }
    },
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": " FORMATO ARP ELECTRONICO",
      "sanic_celda": "B14",
      "sanic_nombre": " FORMATO ARP ELECTRONICO!B14",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Valor simple"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Plazo en cuotas"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      }
    },
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": " FORMATO ARP ELECTRONICO",
      "sanic_celda": "D14",
      "sanic_nombre": " FORMATO ARP ELECTRONICO!D14",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Valor simple"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Cuota mensual"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      }
    },
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": " FORMATO ARP ELECTRONICO",
      "sanic_celda": "F14",
      "sanic_nombre": " FORMATO ARP ELECTRONICO!F14",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Valor simple"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Total a pagar"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      }
    },
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": " FORMATO ARP ELECTRONICO",
      "sanic_celda": "B15",
      "sanic_nombre": " FORMATO ARP ELECTRONICO!B15",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Valor simple"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Fecha de inicio"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      }
    },
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": " FORMATO ARP ELECTRONICO",
      "sanic_celda": "D15",
      "sanic_nombre": " FORMATO ARP ELECTRONICO!D15",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Valor simple"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Fecha de finalizacion"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      }
    },
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": " FORMATO ARP ELECTRONICO",
      "sanic_celda": "F15",
      "sanic_nombre": " FORMATO ARP ELECTRONICO!F15",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Valor simple"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Cuenta a debitar"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      }
    },
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": " FORMATO ARP ELECTRONICO",
      "sanic_celda": "A18",
      "sanic_nombre": " FORMATO ARP ELECTRONICO!A18",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Valor simple"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Comentarios"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      }
    },
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": "CONTROL DE PAGOS",
      "sanic_celda": "B4",
      "sanic_nombre": "CONTROL DE PAGOS!B4",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Valor simple"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Nombre del cliente"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      }
    },
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": "CONTROL DE PAGOS",
      "sanic_celda": "E4",
      "sanic_nombre": "CONTROL DE PAGOS!E4",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Valor simple"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Fecha de formalizacion"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      }
    },
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": "CONTROL DE PAGOS",
      "sanic_celda": "A7",
      "sanic_nombre": "CONTROL DE PAGOS!A7",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Columna de bloque de cuotas"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Cuota: numero"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      },
      "sanic_filasmaximas": 60
    },
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": "CONTROL DE PAGOS",
      "sanic_celda": "B7",
      "sanic_nombre": "CONTROL DE PAGOS!B7",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Columna de bloque de cuotas"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Cuota: fecha de vencimiento"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      },
      "sanic_filasmaximas": 60
    },
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": "CONTROL DE PAGOS",
      "sanic_celda": "C7",
      "sanic_nombre": "CONTROL DE PAGOS!C7",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Columna de bloque de cuotas"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Cuota: monto"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      },
      "sanic_filasmaximas": 60
    },
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": "CONTROL DE PAGOS",
      "sanic_celda": "D7",
      "sanic_nombre": "CONTROL DE PAGOS!D7",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Columna de bloque de cuotas"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Cuota: fecha de pago"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      },
      "sanic_filasmaximas": 60
    },
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": "CONTROL DE PAGOS",
      "sanic_celda": "E7",
      "sanic_nombre": "CONTROL DE PAGOS!E7",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Columna de bloque de cuotas"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Cuota: monto pagado"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      },
      "sanic_filasmaximas": 60
    },
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": "CONTROL DE PAGOS",
      "sanic_celda": "C67",
      "sanic_nombre": "CONTROL DE PAGOS!C67",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Total calculado"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Total: montos de cuota"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      }
    },
    {
      "sanic_versionplantillaid": {
        "lookup": {
          "tabla": "sanic_carp_tbl_versionplantilla",
          "clave": {
            "sanic_nombre": "v07"
          }
        }
      },
      "sanic_hoja": "CONTROL DE PAGOS",
      "sanic_celda": "E67",
      "sanic_nombre": "CONTROL DE PAGOS!E67",
      "sanic_tipo": {
        "choice": "sanic_carp_ch_tipomapeo",
        "etiqueta": "Total calculado"
      },
      "sanic_campo": {
        "choice": "sanic_carp_ch_campomapeable",
        "etiqueta": "Total: montos pagados"
      },
      "ownerid": {
        "lookup": {
          "tabla": "team",
          "clave": {
            "name": "Arreglos de Pago",
            "isdefault": true
          }
        }
      }
    }
  ]
}
```

- La clave de negocio es versión + hoja + celda: las versiones de humo (v90, v91, v94, v95) tienen las mismas celdas, así que la hoja y la celda solas no alcanzan.
- La hoja del formato se llama `" FORMATO ARP ELECTRONICO"`, **con un espacio al inicio** (spike 1, PA-VER-08). Sin el espacio, Publicar rechaza con CARP-VER-009.
- `sanic_nombre` lo calcula el plugin (`<hoja>!<celda>`); el playbook manda el mismo valor para que la verificación lo compare.
- `sanic_filasmaximas` = 60 solo en las 5 columnas del bloque de cuotas.

## 3. Precondiciones

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es managed, y su publisher coincide | los de la sección 1 |
| 2 | Las tablas, los choices y el team propietario existen | `datos.py` los resuelve TODOS antes de escribir la primera fila; si falta uno, `bloqueado` sin tocar nada |
| 3 | La versión `v07` existe y está en Borrador | se carga antes con `versionplantilla.md`; sobre una Vigente el plugin rechaza el alta de un mapeo |

## 4. Cómo se construye

```
python3 herramientas/construir/datos.py playbooks/datos/mapeocelda.md
```

## 5. Verificación

```
python3 herramientas/construir/datos.py playbooks/datos/mapeocelda.md --solo-verificar
```

`ya_existia` con `28 que ya estaban`.
