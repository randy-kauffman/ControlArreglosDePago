# Playbook: steps · sanic_carp_pkg_plugins (37 registros, 21 clases)

> **Tipo con 0 ciclos** (`power-platform-especificar`, `references/step.md`). Un solo playbook con **todos** los steps del paquete: los de un mismo mensaje, tabla y etapa compiten por el orden de ejecución y solo se entienden mirándolos juntos. La entrada `n` del bloque de la sección 2 es la fila `S-n` de `diseno/03-contratos-custom-api.md` §5 (S-01 es la primera; S-37, la última). **S-18 (`ConversacionAsignacionStep`) fue el piloto** del tipo: se escribió primero como un playbook de un solo step (`playbooks/step/ConversacionAsignacionStep.md`) y quedó integrado acá como una entrada más; el piloto se borró.

## 1. Identidad

```json
{
  "tipo_playbook": "steps",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "8.7",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09. La lista de steps es `diseno/03-contratos-custom-api.md` §5 («Lista cerrada de steps a registrar», 37 registros sobre 21 clases) y figura en `diseno/06-inventario-componentes.md` §8, renglón 8.7.

Advertencias: en Dev hay otro publisher con el mismo prefijo `sanic` («Sanic Corp»): la herramienta se detiene si el publisher de la solución no es el de arriba. `tipo_playbook` es `steps`, como lo lee `step.py`.

## 2. Qué se crea

```json
{
  "tipo": "steps",
  "paquete": "sanic_carp_pkg_plugins",
  "steps": [
    {
      "nombre": "STEP - CARP - cliente - Create - PreOperation - PropietarioAlCrearStep",
      "descripcion": "Fija al team del área como propietario de todo cliente nuevo, sin importar quién lo cree.",
      "plugintype": "Sanic.Carp.Plugins.Steps.PropietarioAlCrearStep",
      "mensaje": "Create",
      "tabla": "sanic_carp_tbl_cliente",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 5,
      "filtro": [],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - conversacion - Create - PreOperation - PropietarioAlCrearStep",
      "descripcion": "Fija al team del área como propietario de toda conversación nueva, sin importar quién la cree.",
      "plugintype": "Sanic.Carp.Plugins.Steps.PropietarioAlCrearStep",
      "mensaje": "Create",
      "tabla": "sanic_carp_tbl_conversacion",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 5,
      "filtro": [],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - arreglo - Create - PreOperation - PropietarioAlCrearStep",
      "descripcion": "Fija al team del área como propietario de todo arreglo nuevo, sin importar quién lo cree.",
      "plugintype": "Sanic.Carp.Plugins.Steps.PropietarioAlCrearStep",
      "mensaje": "Create",
      "tabla": "sanic_carp_tbl_arreglo",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 5,
      "filtro": [],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - cuota - Create - PreOperation - PropietarioAlCrearStep",
      "descripcion": "Fija al team del área como propietario de toda cuota nueva, sin importar quién la cree.",
      "plugintype": "Sanic.Carp.Plugins.Steps.PropietarioAlCrearStep",
      "mensaje": "Create",
      "tabla": "sanic_carp_tbl_cuota",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 5,
      "filtro": [],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - versionplantilla - Create - PreOperation - PropietarioAlCrearStep",
      "descripcion": "Fija al team del área como propietario de toda versión de plantilla nueva, sin importar quién la cree.",
      "plugintype": "Sanic.Carp.Plugins.Steps.PropietarioAlCrearStep",
      "mensaje": "Create",
      "tabla": "sanic_carp_tbl_versionplantilla",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 5,
      "filtro": [],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - mapeocelda - Create - PreOperation - PropietarioAlCrearStep",
      "descripcion": "Fija al team del área como propietario de todo mapeo de celda nuevo, sin importar quién lo cree.",
      "plugintype": "Sanic.Carp.Plugins.Steps.PropietarioAlCrearStep",
      "mensaje": "Create",
      "tabla": "sanic_carp_tbl_mapeocelda",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 5,
      "filtro": [],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - generacionformato - Create - PreOperation - PropietarioAlCrearStep",
      "descripcion": "Fija al team del área como propietario de toda generación de formato nueva, sin importar quién la cree.",
      "plugintype": "Sanic.Carp.Plugins.Steps.PropietarioAlCrearStep",
      "mensaje": "Create",
      "tabla": "sanic_carp_tbl_generacionformato",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 5,
      "filtro": [],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - cliente - Update - PreOperation - PropietarioAlActualizarStep",
      "descripcion": "Rechaza que una persona deje como propietario de un cliente a alguien que no sea el team del área.",
      "plugintype": "Sanic.Carp.Plugins.Steps.PropietarioAlActualizarStep",
      "mensaje": "Update",
      "tabla": "sanic_carp_tbl_cliente",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 5,
      "filtro": [
        "ownerid"
      ],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - conversacion - Update - PreOperation - PropietarioAlActualizarStep",
      "descripcion": "Rechaza que una persona deje como propietario de una conversación a alguien que no sea el team del área.",
      "plugintype": "Sanic.Carp.Plugins.Steps.PropietarioAlActualizarStep",
      "mensaje": "Update",
      "tabla": "sanic_carp_tbl_conversacion",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 5,
      "filtro": [
        "ownerid"
      ],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - arreglo - Update - PreOperation - PropietarioAlActualizarStep",
      "descripcion": "Rechaza que una persona deje como propietario de un arreglo a alguien que no sea el team del área.",
      "plugintype": "Sanic.Carp.Plugins.Steps.PropietarioAlActualizarStep",
      "mensaje": "Update",
      "tabla": "sanic_carp_tbl_arreglo",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 5,
      "filtro": [
        "ownerid"
      ],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - cuota - Update - PreOperation - PropietarioAlActualizarStep",
      "descripcion": "Rechaza que una persona deje como propietario de una cuota a alguien que no sea el team del área.",
      "plugintype": "Sanic.Carp.Plugins.Steps.PropietarioAlActualizarStep",
      "mensaje": "Update",
      "tabla": "sanic_carp_tbl_cuota",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 5,
      "filtro": [
        "ownerid"
      ],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - versionplantilla - Update - PreOperation - PropietarioAlActualizarStep",
      "descripcion": "Rechaza que una persona deje como propietario de una versión de plantilla a alguien que no sea el team del área.",
      "plugintype": "Sanic.Carp.Plugins.Steps.PropietarioAlActualizarStep",
      "mensaje": "Update",
      "tabla": "sanic_carp_tbl_versionplantilla",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 5,
      "filtro": [
        "ownerid"
      ],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - mapeocelda - Update - PreOperation - PropietarioAlActualizarStep",
      "descripcion": "Rechaza que una persona deje como propietario de un mapeo de celda a alguien que no sea el team del área.",
      "plugintype": "Sanic.Carp.Plugins.Steps.PropietarioAlActualizarStep",
      "mensaje": "Update",
      "tabla": "sanic_carp_tbl_mapeocelda",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 5,
      "filtro": [
        "ownerid"
      ],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - generacionformato - Update - PreOperation - PropietarioAlActualizarStep",
      "descripcion": "Rechaza que una persona deje como propietario de una generación de formato a alguien que no sea el team del área.",
      "plugintype": "Sanic.Carp.Plugins.Steps.PropietarioAlActualizarStep",
      "mensaje": "Update",
      "tabla": "sanic_carp_tbl_generacionformato",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 5,
      "filtro": [
        "ownerid"
      ],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - cliente - Create - PreOperation - ClienteStep",
      "descripcion": "Normaliza y valida el nombre, la cédula, el correo y el celular del cliente al crearlo, exige la cédula y rechaza una cédula ya registrada.",
      "plugintype": "Sanic.Carp.Plugins.Steps.ClienteStep",
      "mensaje": "Create",
      "tabla": "sanic_carp_tbl_cliente",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 10,
      "filtro": [],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - cliente - Update - PreOperation - ClienteStep",
      "descripcion": "Normaliza y valida los datos del cliente al cambiarlos, rechaza que una persona cambie su estado o su nombre mostrado y vuelve a comprobar que la cédula no esté repetida.",
      "plugintype": "Sanic.Carp.Plugins.Steps.ClienteStep",
      "mensaje": "Update",
      "tabla": "sanic_carp_tbl_cliente",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 10,
      "filtro": [
        "sanic_nombre",
        "sanic_nombrecompleto",
        "sanic_cedula",
        "sanic_correo",
        "sanic_celular",
        "statecode",
        "statuscode"
      ],
      "preimagen": {
        "alias": "PreImagen",
        "columnas": [
          "sanic_nombre",
          "statecode",
          "statuscode"
        ]
      }
    },
    {
      "nombre": "STEP - CARP - conversacion - Update - PreOperation - ConversacionActualizarStep",
      "descripcion": "Gobierna los cambios de una conversación: aplica las transiciones de estado (finalizar y reabrir), protege las columnas que solo escribe el sistema y bloquea cambios en una conversación finalizada o unida.",
      "plugintype": "Sanic.Carp.Plugins.Steps.ConversacionActualizarStep",
      "mensaje": "Update",
      "tabla": "sanic_carp_tbl_conversacion",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 10,
      "filtro": [],
      "preimagen": {
        "alias": "PreImagen",
        "columnas": [
          "statecode",
          "statuscode",
          "sanic_huellamessageid",
          "sanic_fechaprimercorreo",
          "sanic_asunto",
          "sanic_remitente",
          "sanic_sinrespuestadesde",
          "sanic_correonuevoenfinalizada",
          "sanic_resultado",
          "sanic_motivosinrespuesta",
          "sanic_detallesinrespuesta",
          "sanic_fechafinalizacion",
          "sanic_tiempoprimerarespuesta",
          "sanic_tiempototal",
          "sanic_cantidadrespuestas",
          "sanic_unidaaid",
          "sanic_finalizadoporid",
          "sanic_clienteid",
          "sanic_nombre",
          "sanic_oficialasignadoid",
          "ownerid"
        ]
      }
    },
    {
      "nombre": "STEP - CARP - conversacion - Update - PreOperation - ConversacionAsignacionStep",
      "descripcion": "Controla quién puede cambiar el oficial asignado de una conversación: tomarla vacía pasa; cualquier otro cambio exige ser de la jefatura.",
      "plugintype": "Sanic.Carp.Plugins.Steps.ConversacionAsignacionStep",
      "mensaje": "Update",
      "tabla": "sanic_carp_tbl_conversacion",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 20,
      "filtro": [
        "sanic_oficialasignadoid"
      ],
      "preimagen": {
        "alias": "PreImagen",
        "columnas": [
          "sanic_oficialasignadoid"
        ]
      }
    },
    {
      "nombre": "STEP - CARP - arreglo - Create - PreOperation - ArregloCrearStep",
      "descripcion": "Valida el alta de un arreglo hecha por una persona (siempre en Borrador), completa la versión vigente de la plantilla, calcula los montos y la fecha de fin y rechaza las columnas que solo escribe el sistema.",
      "plugintype": "Sanic.Carp.Plugins.Steps.ArregloCrearStep",
      "mensaje": "Create",
      "tabla": "sanic_carp_tbl_arreglo",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 10,
      "filtro": [],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - arreglo - Create - PostOperation - ArregloCalendarioStep",
      "descripcion": "Crea el calendario de cuotas de un arreglo recién creado; si falla, el arreglo tampoco se guarda.",
      "plugintype": "Sanic.Carp.Plugins.Steps.ArregloCalendarioStep",
      "mensaje": "Create",
      "tabla": "sanic_carp_tbl_arreglo",
      "etapa": "PostOperation",
      "modo": "Sincrono",
      "orden": 10,
      "filtro": [],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - arreglo - Update - PreOperation - ArregloActualizarStep",
      "descripcion": "Gobierna los cambios de un arreglo: identifica el gesto, aplica la máquina de estados, protege las columnas congeladas y las que solo escribe el sistema, y rechaza cambios en un arreglo cerrado.",
      "plugintype": "Sanic.Carp.Plugins.Steps.ArregloActualizarStep",
      "mensaje": "Update",
      "tabla": "sanic_carp_tbl_arreglo",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 10,
      "filtro": [],
      "preimagen": {
        "alias": "PreImagen",
        "columnas": [
          "statecode",
          "statuscode",
          "sanic_nombre",
          "sanic_producto",
          "sanic_numeroproducto",
          "sanic_numeroarreglo",
          "sanic_numeroprovisional",
          "sanic_tipo",
          "sanic_moneda",
          "sanic_canalorigen",
          "sanic_saldoinicial",
          "sanic_montoabonado",
          "sanic_saldoremanente",
          "sanic_plazo",
          "sanic_cuotamensual",
          "sanic_totalpagar",
          "sanic_fechainicio",
          "sanic_fechafin",
          "sanic_cuentadebitar",
          "sanic_fechaformalizacion",
          "sanic_comentarios",
          "sanic_fechacongelamiento",
          "sanic_instantaneanombre",
          "sanic_instantaneacedula",
          "sanic_instantaneacorreo",
          "sanic_instantaneacelular",
          "sanic_fechaformalizado",
          "sanic_formalizadoporid",
          "sanic_motivodescarte",
          "sanic_fechadescarte",
          "sanic_descartadoporid",
          "sanic_motivoanulacion",
          "sanic_fechaanulacion",
          "sanic_anuladoporid",
          "sanic_motivoreapertura",
          "sanic_fechareapertura",
          "sanic_reabiertoporid",
          "sanic_cantidadreaperturas",
          "sanic_fechatermino",
          "sanic_versionplantillaid",
          "sanic_clienteid",
          "sanic_conversacionid",
          "sanic_oficialid"
        ]
      }
    },
    {
      "nombre": "STEP - CARP - arreglo - Update - PostOperation - ArregloCalendarioStep",
      "descripcion": "Recalcula el calendario de cuotas cuando cambia el plazo, la cuota mensual, la fecha de inicio, el saldo inicial o el monto abonado de un arreglo.",
      "plugintype": "Sanic.Carp.Plugins.Steps.ArregloCalendarioStep",
      "mensaje": "Update",
      "tabla": "sanic_carp_tbl_arreglo",
      "etapa": "PostOperation",
      "modo": "Sincrono",
      "orden": 10,
      "filtro": [
        "sanic_plazo",
        "sanic_cuotamensual",
        "sanic_fechainicio",
        "sanic_saldoinicial",
        "sanic_montoabonado"
      ],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - cuota - Update - PreOperation - CuotaActualizarStep",
      "descripcion": "Limita el cambio de una cuota a registrar el pago (fecha y monto), solo en un arreglo Vigente, y rechaza cualquier otra columna.",
      "plugintype": "Sanic.Carp.Plugins.Steps.CuotaActualizarStep",
      "mensaje": "Update",
      "tabla": "sanic_carp_tbl_cuota",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 10,
      "filtro": [],
      "preimagen": {
        "alias": "PreImagen",
        "columnas": [
          "sanic_nombre",
          "sanic_numero",
          "sanic_fechavencimiento",
          "sanic_monto",
          "sanic_fechapago",
          "sanic_montopagado",
          "sanic_arregloid",
          "statecode",
          "statuscode"
        ]
      }
    },
    {
      "nombre": "STEP - CARP - cuota - Update - PostOperation - CuotaTerminarArregloStep",
      "descripcion": "Cuando se registra el último pago pendiente de un arreglo Vigente, lo pasa a Terminado como escritura del sistema.",
      "plugintype": "Sanic.Carp.Plugins.Steps.CuotaTerminarArregloStep",
      "mensaje": "Update",
      "tabla": "sanic_carp_tbl_cuota",
      "etapa": "PostOperation",
      "modo": "Sincrono",
      "orden": 20,
      "filtro": [
        "sanic_fechapago",
        "sanic_montopagado"
      ],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - versionplantilla - Create - PreOperation - VersionPlantillaCrearStep",
      "descripcion": "Obliga a que una versión de plantilla nazca en Borrador, sin huella ni datos de aprobación y con un nombre del formato vNN.",
      "plugintype": "Sanic.Carp.Plugins.Steps.VersionPlantillaCrearStep",
      "mensaje": "Create",
      "tabla": "sanic_carp_tbl_versionplantilla",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 10,
      "filtro": [],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - versionplantilla - Update - PreOperation - VersionPlantillaActualizarStep",
      "descripcion": "Gobierna el ciclo de la versión de plantilla (Borrador, Vigente, Histórica): al publicar valida el mapeo, genera una prueba y deja una sola Vigente; el estado nativo solo lo cambia el sistema.",
      "plugintype": "Sanic.Carp.Plugins.Steps.VersionPlantillaActualizarStep",
      "mensaje": "Update",
      "tabla": "sanic_carp_tbl_versionplantilla",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 10,
      "filtro": [],
      "preimagen": {
        "alias": "PreImagen",
        "columnas": [
          "statecode",
          "statuscode",
          "sanic_nombre",
          "sanic_estado",
          "sanic_capacidad",
          "sanic_huella",
          "sanic_fechaaprobacion",
          "sanic_aprobadoporid"
        ]
      }
    },
    {
      "nombre": "STEP - CARP - versionplantilla - Update - PreOperation - ArchivoDePlantillaStep",
      "descripcion": "Rechaza que una persona cambie el archivo de una versión de plantilla que no está en Borrador.",
      "plugintype": "Sanic.Carp.Plugins.Steps.ArchivoDePlantillaStep",
      "mensaje": "Update",
      "tabla": "sanic_carp_tbl_versionplantilla",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 20,
      "filtro": [
        "sanic_archivo"
      ],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - mapeocelda - Create - PreOperation - MapeoCeldaCrearStep",
      "descripcion": "Rechaza que una persona agregue un mapeo a una versión de plantilla que no está en Borrador, valida el mapeo y calcula su nombre.",
      "plugintype": "Sanic.Carp.Plugins.Steps.MapeoCeldaCrearStep",
      "mensaje": "Create",
      "tabla": "sanic_carp_tbl_mapeocelda",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 10,
      "filtro": [],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - mapeocelda - Update - PreOperation - MapeoCeldaActualizarStep",
      "descripcion": "Rechaza que una persona cambie el mapeo de una versión de plantilla que no está en Borrador, protege el estado y el nombre calculado y valida los valores del mapeo.",
      "plugintype": "Sanic.Carp.Plugins.Steps.MapeoCeldaActualizarStep",
      "mensaje": "Update",
      "tabla": "sanic_carp_tbl_mapeocelda",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 10,
      "filtro": [],
      "preimagen": {
        "alias": "PreImagen",
        "columnas": [
          "sanic_versionplantillaid",
          "sanic_hoja",
          "sanic_celda",
          "sanic_tipo",
          "sanic_campo",
          "sanic_filasmaximas",
          "statecode",
          "statuscode",
          "sanic_nombre"
        ]
      }
    },
    {
      "nombre": "STEP - CARP - mapeocelda - Delete - PreOperation - MapeoCeldaEliminarStep",
      "descripcion": "Rechaza que una persona elimine el mapeo de una versión de plantilla que no está en Borrador.",
      "plugintype": "Sanic.Carp.Plugins.Steps.MapeoCeldaEliminarStep",
      "mensaje": "Delete",
      "tabla": "sanic_carp_tbl_mapeocelda",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 10,
      "filtro": [],
      "preimagen": {
        "alias": "PreImagen",
        "columnas": [
          "sanic_versionplantillaid"
        ]
      }
    },
    {
      "nombre": "STEP - CARP - email - Create - PreOperation - RemitenteDelAreaStep",
      "descripcion": "Obliga a que todo correo saliente de una conversación salga desde el buzón del área, reemplazando el remitente por defecto.",
      "plugintype": "Sanic.Carp.Plugins.Steps.RemitenteDelAreaStep",
      "mensaje": "Create",
      "tabla": "email",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 10,
      "filtro": [],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - email - Create - PreOperation - EmailSoloSistemaStep",
      "descripcion": "Rechaza que una persona cree un correo con las columnas que solo escribe el sistema (tiempo de respuesta y quién respondió).",
      "plugintype": "Sanic.Carp.Plugins.Steps.EmailSoloSistemaStep",
      "mensaje": "Create",
      "tabla": "email",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 20,
      "filtro": [],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - email - Update - PreOperation - EmailSoloSistemaStep",
      "descripcion": "Rechaza que una persona cambie en un correo las columnas que solo escribe el sistema (tiempo de respuesta y quién respondió).",
      "plugintype": "Sanic.Carp.Plugins.Steps.EmailSoloSistemaStep",
      "mensaje": "Update",
      "tabla": "email",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 10,
      "filtro": [
        "sanic_tiemporespuesta",
        "sanic_respondidoporid"
      ],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - email - Create - PostOperation - RegistrarCorreoEntranteStep",
      "descripcion": "Registra cada correo entrante en una conversación, nueva o existente, de forma asíncrona; si falla, el correo queda guardado y la reconciliación lo recoge.",
      "plugintype": "Sanic.Carp.Plugins.Steps.RegistrarCorreoEntranteStep",
      "mensaje": "Create",
      "tabla": "email",
      "etapa": "PostOperation",
      "modo": "Asincrono",
      "orden": 10,
      "filtro": [],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - email - Update - PostOperation - RespuestaEnviadaStep",
      "descripcion": "Cuando se envía una respuesta, actualiza de forma asíncrona la cantidad de respuestas, los tiempos y los pendientes de la conversación.",
      "plugintype": "Sanic.Carp.Plugins.Steps.RespuestaEnviadaStep",
      "mensaje": "Update",
      "tabla": "email",
      "etapa": "PostOperation",
      "modo": "Asincrono",
      "orden": 10,
      "filtro": [
        "statuscode"
      ],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - conversacion - Create - PreOperation - ConversacionCrearStep",
      "descripcion": "Rechaza que una persona escriba el código autonumérico de la conversación, que asigna la plataforma.",
      "plugintype": "Sanic.Carp.Plugins.Steps.ConversacionCrearStep",
      "mensaje": "Create",
      "tabla": "sanic_carp_tbl_conversacion",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 10,
      "filtro": [],
      "preimagen": null
    },
    {
      "nombre": "STEP - CARP - email - Update - PreOperation - RemitenteDelAreaStep",
      "descripcion": "Exige que el remitente de un correo saliente de una conversación sea el buzón del área también al actualizarlo, para que nadie eluda el control del alta.",
      "plugintype": "Sanic.Carp.Plugins.Steps.RemitenteDelAreaStep",
      "mensaje": "Update",
      "tabla": "email",
      "etapa": "PreOperation",
      "modo": "Sincrono",
      "orden": 20,
      "filtro": [
        "from",
        "directioncode",
        "regardingobjectid"
      ],
      "preimagen": null
    }
  ]
}
```

Cada entrada copia una fila de `diseno/03` §5 (clase, mensaje, tabla, etapa, modo, orden y filtro) y, donde la fila trae pre-imagen, las columnas de la tabla «Pre-imágenes con sus columnas» de la misma sección. La pre-imagen siempre se llama `PreImagen`: el código la lee por ese nombre y, sin ella, el step rechaza el registro antes de evaluar nada. El diseño abrevia `sanic_carp_tbl_` como `t_` en esa tabla; acá va el nombre lógico completo, y `email` es la tabla estándar. Los valores (mensaje, etapa, modo, orden, filtro y columnas de la pre-imagen) coinciden también con el registro que declara cada clase en `src/Sanic.Carp.Plugins/Steps/*.cs` (`RegistroEsperado`).

**Con quién corre cada step.** Con el usuario que llama: es el valor por defecto de la plataforma y `§5` no pide otro. `diseno/03` §5 no trae un «usuario de ejecución» para ninguna fila, y tampoco `§0.2`: los steps leen la identidad real (`InitiatingUserId`, `§0.3`) y, cuando tienen que escribir como sistema, lo hacen con un servicio de SYSTEM que crean dentro del código. Los dos asíncronos (S-34 y S-35; `§0.2` los lista con lectura y escritura de SYSTEM) también se registran con el valor por defecto y elevan en el código. Esto **no es una clave del bloque**: la herramienta no la lee ni la escribe (`references/step.md` §6). Si alguna vez se pidiera un usuario fijo (`impersonatinguserid`) habría que ampliar herramienta y formato.

**Los dos asíncronos y el borrado del trabajo del sistema.** S-34 (`RegistrarCorreoEntranteStep`, Create de `email`) y S-35 (`RespuestaEnviadaStep`, Update de `email`) son `PostOperation` asíncronos. **Hueco de diseño:** ni `§5` ni `§2.10`/`§2.11` dicen si el trabajo del sistema (`asyncoperation`) se borra al terminar bien (`asyncautodelete`), y la herramienta tampoco lo escribe: queda el valor por defecto de la plataforma. Un asíncrono no revierte nada y su fallo no lo ve el usuario; el diseño lo cubre con la reconciliación (`sanic_carp_capi_reconciliarcorreos`, inventario 8.3) y con el aviso del flujo de salud (inventario 11.2). El dueño decide si se borran los trabajos exitosos antes de que el volumen de correo los acumule.

**Nombres.** `STEP - CARP - <tabla limpia> - <Mensaje> - <Etapa> - <Clase>`: la tabla es el nombre de negocio sin prefijo ni tildes (`conversacion`, `versionplantilla`, `mapeocelda`, `generacionformato`; `email` es las dos cosas), como en el piloto S-18, y la clase al final es la de `diseno/03` DC-23. Como la clave (clase, mensaje, tabla) es única en los 37 registros (`DC-27`), **ningún par de registros da el mismo nombre** y no hizo falta ningún desempate; si una fila futura repitiera clase, mensaje y tabla, la herramienta la rechazaría antes que el nombre.

**Orden de ejecución por (mensaje, tabla, etapa)** (BP-PP-060; ningún par repite el orden, y `§5` «Orden de ejecución» dice lo mismo):

| Mensaje · tabla · etapa | Órdenes (steps) |
|---|---|
| Create · `cliente` · PreOperation | 5 (S-01), 10 (S-15) |
| Create · `conversacion` · PreOperation | 5 (S-02), 10 (S-36) |
| Create · `arreglo` · PreOperation | 5 (S-03), 10 (S-19) |
| Create · `arreglo` · PostOperation | 10 (S-20) |
| Create · `cuota` · PreOperation | 5 (S-04) |
| Create · `versionplantilla` · PreOperation | 5 (S-05), 10 (S-25) |
| Create · `mapeocelda` · PreOperation | 5 (S-06), 10 (S-28) |
| Create · `generacionformato` · PreOperation | 5 (S-07) |
| Create · `email` · PreOperation | 10 (S-31), 20 (S-32) |
| Create · `email` · PostOperation asíncrono | 10 (S-34) |
| Update · `cliente` · PreOperation | 5 (S-08), 10 (S-16) |
| Update · `conversacion` · PreOperation | 5 (S-09), 10 (S-17), 20 (S-18) |
| Update · `arreglo` · PreOperation | 5 (S-10), 10 (S-21) |
| Update · `arreglo` · PostOperation | 10 (S-22) |
| Update · `cuota` · PreOperation | 5 (S-11), 10 (S-23) |
| Update · `cuota` · PostOperation | 20 (S-24) |
| Update · `versionplantilla` · PreOperation | 5 (S-12), 10 (S-26), 20 (S-27) |
| Update · `mapeocelda` · PreOperation | 5 (S-13), 10 (S-29) |
| Update · `generacionformato` · PreOperation | 5 (S-14) |
| Update · `email` · PreOperation | 10 (S-33), 20 (S-37) |
| Update · `email` · PostOperation asíncrono | 10 (S-35) |
| Delete · `mapeocelda` · PreOperation | 10 (S-30) |

El propietario (orden 5) corre antes que las reglas de la tabla (10) para que ninguna regla vea un propietario ajeno (`§5`). S-37 lleva el orden 20 y no el 10 que había pedido el usuario porque S-33 ya ocupa el 10 de `Update` Pre sobre `email` (nota de `§5`).

Decisiones que este playbook toma y el diseño no traía:

| Decisión | Valor | Por qué |
|---|---|---|
| `tabla` en el nombre del step | Nombre de negocio sin prefijo (`conversacion`) | **Hueco de convención:** `diseno/01-convenciones.md` §2 y DC-23 dicen `STEP - CARP - tabla - mensaje - etapa - clase` sin aclarar si «tabla» es el nombre lógico completo o el limpio, y BP-PP-183 tampoco. Se usó el limpio, igual que el piloto S-18 y el fixture de pruebas de la herramienta. El dueño lo confirma; el nombre es el identificador visible del step (BP-PP-185) y se corrige con `--completar` |
| Descripción de cada step | Una oración con lo que hace, con ortografía completa | `diseno/03` no trae el texto; cada una resume la regla de la sección de la clase (`§2.1` a `§2.11`) |
| Mayúsculas de mensaje y etapa | `Create`, `Update`, `Delete`; `PreOperation`, `PostOperation` | Son los valores que lee `step.py` (`ETAPAS`, `PROPIEDAD_DE_IMAGEN`) |
| Entrada de pre-imagen en `Delete` (S-30) | `PreImagen` con `sanic_versionplantillaid` | La fila de `§5` la pide y `step.py` admite imágenes en `Delete` (propiedad `Target`) |

Huecos de diseño que este playbook no cierra:

- **Usuario de ejecución y borrado automático de los asíncronos**: ver los dos párrafos de arriba. El diseño no los trae y la herramienta no los escribe; el playbook no inventa un valor.
- **Seguridad por columna en la pre-imagen.** La pre-imagen de S-21 trae `sanic_cuentadebitar` e `sanic_instantaneacedula`, que tienen perfil de columna: quien no lo tiene recibe la imagen **sin** ellas (ausentes, no nulas). El código lo trata (`diseno/03` DC-01 y DC-28: «ausente es desconocido» y las lee con SYSTEM); en Conversación, que no tiene seguridad por columna, una columna ausente vale nulo (`§2.3`). Queda sin medir con el perfil asociado por team (riesgo H-13).
- **Pruebas en Dev pendientes** que afectan a estos registros: H-19 (a) (los steps ven SYSTEM en las escrituras anidadas) y V-1 (un cambio de estado llega como `Update` anidado, por eso no se registra ningún step de `SetState`).

## 3. Precondiciones

| # | Qué tiene que cumplirse | Consulta | Esperado |
|---|---|---|---|
| 1 | La solución existe, no es managed, su publisher coincide en unique name, prefijo y prefijo de opciones; cada idioma está provisionado y el idioma base es uno de ellos | `solutions?$filter=uniquename eq '<solucion>'` con el publisher expandido; `organizations`; `RetrieveProvisionedLanguages` | los de la sección 1 |
| 2 | Cada una de las **21** clases está registrada **una sola vez** (las 21 de `Sanic.Carp.Plugins.Steps.*` figuran en `tipos` del playbook del paquete) | `plugintypes?$filter=typename eq '<plugintype>'` | una fila por clase (las crea el playbook del paquete, inventario 8.1) |
| 3 | Los mensajes `Create`, `Update` y `Delete` existen una sola vez | `sdkmessages?$filter=name eq '<mensaje>'` | una fila cada uno |
| 4 | Cada tabla admite su mensaje | `sdkmessagefilters?$filter=primaryobjecttypecode eq '<tabla>'` | hay uno por cada par (mensaje, tabla) del bloque; las siete tablas propias son las del inventario 3.x y `email` es estándar |
| 5 | Cada columna de un filtro y de una pre-imagen existe en su tabla; ningún filtro nombra la clave primaria (`<tabla>id`; en `email`, **`activityid`**) | diccionario de datos y metadatos de la tabla | existen; ninguna es clave primaria. Ningún filtro del bloque nombra una |
| 6 | Ningún nombre lleva tildes ni ñ y todos miden 256 caracteres o menos; ninguna clave (plugintype, mensaje, tabla) se repite | sin red | los de la sección 2 |
| 7 | Los dos asíncronos van solo en PostOperation | sin red | S-34 y S-35 |
| 8 | **El hueco del nombre (qué es «tabla» en el nombre del step) está cerrado por el dueño** | decisión escrita en `diseno/01-convenciones.md` §2 | confirmación; mientras no esté, el constructor no corre |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/dataverse/patrones.md`, «Registro de steps desde un playbook». Herramienta del proyecto: `herramientas/construir/step.py`. **Un solo comando registra los 37 steps**, en el orden de la lista, con su pre-imagen donde la lleva.

```
python3 herramientas/construir/step.py playbooks/steps/sanic_carp_pkg_plugins.md
```

## 5. Verificación

```
python3 herramientas/construir/step.py playbooks/steps/sanic_carp_pkg_plugins.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | Por cada una de las 37 entradas, `GET sdkmessageprocessingsteps` por tipo de plugin, mensaje y filtro de mensaje de la tabla | **un** step · `name` y `description` de la sección 2 · `stage` (PreOperation = 20, PostOperation = 40) · `mode` (síncrono = 0, asíncrono = 1) · `rank` · `filteringattributes` (sin importar el orden de las columnas; sin filtro, nulo) · habilitado (`statecode = 0`) |
| 2 | Por cada entrada con `preimagen` (S-16, S-17, S-18, S-21, S-23, S-26, S-29 y S-30), `GET sdkmessageprocessingstepimages` | **una** imagen de tipo pre (`imagetype = 0`) · `entityalias` `PreImagen` · `attributes` con las columnas del bloque (sin importar el orden) · `messagepropertyname = Target`. Para las 29 entradas sin pre-imagen, **ninguna**: una de más es `difiere` y no se borra sola |
| 3 | Pertenencia a la solución | cada step figura en `solutioncomponents` de la solución; se informa el `componenttype` |
| 4 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0 |

Lo que **esta verificación no mira** (ver `references/step.md` §6) y se revisa a mano con `GET sdkmessageprocessingsteps` sobre los tipos del paquete:

- Steps de más: un step registrado sobre las clases del paquete que ninguna entrada declare no se denuncia. Esperado: **37** steps sobre las 21 clases de `Steps.*` (los 5 steps internos de las Custom API, sobre las clases `Api.*`, los crea la plataforma y no cuentan).
- Orden único por (mensaje, tabla, etapa) contra el entorno: la herramienta no lo compara. Esperado: la tabla de órdenes de la sección 2, sin repetidos.
- Que ningún filtro de `email` nombre `activityid`: la herramienta calcula la clave primaria como `<tabla>id` y para `email` esa cuenta daría `emailid`, que no es la clave. El bloque no tiene ese caso, pero la herramienta no lo atraparía.

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5, entrada por entrada. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**. Con `--completar` la herramienta corrige nombre, descripción, etapa, modo, orden y filtro de un step ya registrado, y agrega la pre-imagen si falta; nunca cambia el tipo de plugin, el mensaje ni la tabla (eso es otro step) y nunca borra. Lo decide quien dirige la construcción.

## 7. Reversa

Eliminar un step (`DELETE sdkmessageprocessingsteps(<id>)`; sus imágenes caen con él) lo hace una persona: la herramienta nunca borra. Sin los steps, las reglas de negocio que viven solo en ellos dejan de aplicarse (propietario, estados cerrados, columnas solo-sistema, la jefatura para reasignar, el remitente del área); en particular, sin S-18 **cualquier persona con `Write` sobre Conversación puede reasignar el oficial** (D-04). Sin S-34 los correos entrantes dejan de registrarse solos y solo los recoge la reconciliación. Nada de esto es irreversible: nombre, descripción, etapa, modo, orden y filtro se corrigen; la clave natural (clase, mensaje, tabla) no, y cambiarla es otro step.

## 8. Fuera de alcance

- Registrar el paquete y los 26 tipos (21 de step y 5 de Custom API): inventario 8.1 (`playbooks/paquete-plugins/Sanic.Carp.Plugins.md`).
- Las cinco Custom API, cuyo step interno crea la plataforma: inventario 8.2 a 8.6 (`playbooks/custom-api/`). En particular `sanic_carp_capi_tomarconversacion` (8.4) hace el `Update` que S-18 deja pasar (de vacío a uno mismo).
- El código de los steps (inventario 7.x) y sus pruebas.
- El team de jefatura y la variable de entorno que lo nombra (inventario 13 y 10.2), el buzón del área (10.8) y el flujo de salud (11.2).
- Un usuario de ejecución distinto del llamador, el borrado automático del trabajo asíncrono, la post-imagen y la configuración segura del step: el formato y la herramienta no los admiten y el diseño no los pide.
