using System;
using System.Collections.Generic;

namespace Sanic.Carp.Plugins.Dominio.Errores
{
    /// <summary>
    /// Un codigo <c>CARP-AREA-NNN</c> por cada fila de las tablas de errores de diseno/03; una prueba de aceptacion
    /// (<c>CodigosDeErrorAceptacion</c>) lo contrasta con el documento. Si se agrega una fila al documento, hay que agregar aqui su
    /// entrada. Los textos <c>CARP-CFG-*</c> no estan en el documento, que solo dice que "nombran la variable o el elemento que falta,
    /// nunca un valor" (0.4): los fijo el arquitecto.
    /// </summary>
    public static class CodigosDeError
    {
        public static readonly DefinicionDeError GEN_001 = new DefinicionDeError("CARP-GEN-001", "Falta el arreglo que se quiere imprimir.");
        public static readonly DefinicionDeError GEN_002 = new DefinicionDeError("CARP-GEN-002", "No se encontró el arreglo o no tiene acceso a él.");
        public static readonly DefinicionDeError GEN_003 = new DefinicionDeError("CARP-GEN-003", "La versión de plantilla no tiene archivo cargado.");
        public static readonly DefinicionDeError GEN_004 = new DefinicionDeError("CARP-GEN-004", "El plazo del arreglo ({plazo}) supera la capacidad de la plantilla ({capacidad} cuotas).");
        public static readonly DefinicionDeError GEN_005 = new DefinicionDeError("CARP-GEN-005", "El calendario del arreglo está incompleto: hay {n} cuotas y el plazo es {plazo}.");
        public static readonly DefinicionDeError GEN_006 = new DefinicionDeError("CARP-GEN-006", "La vista previa solo admite una versión en estado Borrador.");
        public static readonly DefinicionDeError GEN_007 = new DefinicionDeError("CARP-GEN-007", "No tiene permiso para previsualizar versiones de plantilla.");
        public static readonly DefinicionDeError GEN_008 = new DefinicionDeError("CARP-GEN-008", "La plantilla no coincide con la huella guardada al publicar la versión. Fue alterada o no es la versión publicada.");
        public static readonly DefinicionDeError GEN_009 = new DefinicionDeError("CARP-GEN-009", "El archivo de la plantilla no es un libro de Excel válido.");
        public static readonly DefinicionDeError GEN_010 = new DefinicionDeError("CARP-GEN-010", "El mapeo de la versión es inválido: {detalle}.");
        public static readonly DefinicionDeError GEN_011 = new DefinicionDeError("CARP-GEN-011", "Los datos del arreglo no sirven para esta plantilla: {detalle}.");
        public static readonly DefinicionDeError GEN_900 = new DefinicionDeError("CARP-GEN-900", "No se pudo generar el formato. No se guardó ningún cambio. Intente de nuevo.");
        public static readonly DefinicionDeError REC_001 = new DefinicionDeError("CARP-REC-001", "El máximo de correos no puede ser negativo.");
        public static readonly DefinicionDeError REC_002 = new DefinicionDeError("CARP-REC-002", "Los minutos mínimos no pueden ser negativos.");
        public static readonly DefinicionDeError REC_003 = new DefinicionDeError("CARP-REC-003", "El correo indicado no existe o no es un correo entrante sin conversación.");
        public static readonly DefinicionDeError REC_900 = new DefinicionDeError("CARP-REC-900", "No se pudo completar la reconciliación. No se guardó ningún cambio del lote.");
        public static readonly DefinicionDeError TOM_001 = new DefinicionDeError("CARP-TOM-001", "Falta la conversación que se quiere tomar.");
        public static readonly DefinicionDeError TOM_002 = new DefinicionDeError("CARP-TOM-002", "No se encontró la conversación o no tiene acceso a ella.");
        public static readonly DefinicionDeError TOM_003 = new DefinicionDeError("CARP-TOM-003", "Solo se puede tomar una conversación abierta.");
        public static readonly DefinicionDeError TOM_004 = new DefinicionDeError("CARP-TOM-004", "La conversación ya fue tomada por {oficial}.");
        public static readonly DefinicionDeError TOM_900 = new DefinicionDeError("CARP-TOM-900", "La conversación cambió en este instante. Actualice la pantalla y vuelva a intentar.");
        public static readonly DefinicionDeError UNI_001 = new DefinicionDeError("CARP-UNI-001", "Faltan la conversación de origen o la de destino.");
        public static readonly DefinicionDeError UNI_002 = new DefinicionDeError("CARP-UNI-002", "No se puede unir una conversación consigo misma.");
        public static readonly DefinicionDeError UNI_003 = new DefinicionDeError("CARP-UNI-003", "No se encontró alguna de las conversaciones o no tiene acceso a ella.");
        public static readonly DefinicionDeError UNI_004 = new DefinicionDeError("CARP-UNI-004", "La conversación de origen debe estar abierta.");
        public static readonly DefinicionDeError UNI_005 = new DefinicionDeError("CARP-UNI-005", "La conversación de destino debe estar abierta.");
        public static readonly DefinicionDeError UNI_006 = new DefinicionDeError("CARP-UNI-006", "Una de las conversaciones ya está unida a otra. No se permiten uniones encadenadas.");
        public static readonly DefinicionDeError UNI_007 = new DefinicionDeError("CARP-UNI-007", "La unión formaría un ciclo entre conversaciones.");
        public static readonly DefinicionDeError UNI_008 = new DefinicionDeError("CARP-UNI-008", "La conversación de origen ya recibió otras conversaciones. Una unión sobre otra unión no está permitida: una el destino sobre el origen o elija otro destino.");
        public static readonly DefinicionDeError UNI_009 = new DefinicionDeError("CARP-UNI-009", "La conversación de origen tiene más de 1.000 correos y no se puede unir en una sola operación.");
        public static readonly DefinicionDeError UNI_010 = new DefinicionDeError("CARP-UNI-010", "Las dos conversaciones son de clientes distintos. Solo se pueden unir conversaciones del mismo cliente.");
        public static readonly DefinicionDeError UNI_900 = new DefinicionDeError("CARP-UNI-900", "No se pudo unir. No se guardó ningún cambio. Intente de nuevo.");
        public static readonly DefinicionDeError INI_001 = new DefinicionDeError("CARP-INI-001", "Falta la conversación.");
        public static readonly DefinicionDeError INI_002 = new DefinicionDeError("CARP-INI-002", "No se encontró la conversación o no tiene acceso a ella.");
        public static readonly DefinicionDeError INI_003 = new DefinicionDeError("CARP-INI-003", "Solo una conversación finalizada con correos nuevos puede iniciar una conversación nueva.");
        public static readonly DefinicionDeError INI_004 = new DefinicionDeError("CARP-INI-004", "La conversación está marcada con correos nuevos, pero no se encontró ninguno posterior a la finalización. Avise al equipo de soporte.");
        public static readonly DefinicionDeError INI_005 = new DefinicionDeError("CARP-INI-005", "Ya existe una conversación para ese correo.");
        public static readonly DefinicionDeError INI_006 = new DefinicionDeError("CARP-INI-006", "Hay más de 1.000 correos nuevos y no se pueden mover en una sola operación.");
        public static readonly DefinicionDeError INI_900 = new DefinicionDeError("CARP-INI-900", "No se pudo iniciar la conversación nueva. No se guardó ningún cambio. Intente de nuevo.");
        public static readonly DefinicionDeError PRO_001 = new DefinicionDeError("CARP-PRO-001", "Los registros de esta solución pertenecen siempre al equipo del área. No se puede cambiar el propietario.");
        public static readonly DefinicionDeError CLI_001 = new DefinicionDeError("CARP-CLI-001", "El nombre completo del cliente es obligatorio.");
        public static readonly DefinicionDeError CLI_002 = new DefinicionDeError("CARP-CLI-002", "La cédula del cliente es obligatoria.");
        public static readonly DefinicionDeError CLI_003 = new DefinicionDeError("CARP-CLI-003", "Ya existe un cliente con esa cédula.");
        public static readonly DefinicionDeError CLI_004 = new DefinicionDeError("CARP-CLI-004", "Un cliente no se activa ni se desactiva a mano: lo gestiona el sistema.");
        public static readonly DefinicionDeError CLI_005 = new DefinicionDeError("CARP-CLI-005", "El nombre del cliente lo calcula el sistema a partir del nombre completo y no se puede cambiar a mano. Si necesita otro nombre, cambie el nombre completo.");
        public static readonly DefinicionDeError CNV_001 = new DefinicionDeError("CARP-CNV-001", "Estas columnas las gestiona el sistema y no se pueden modificar: {columnas}.");
        public static readonly DefinicionDeError CNV_002 = new DefinicionDeError("CARP-CNV-002", "Esa transición de estado no está permitida.");
        public static readonly DefinicionDeError CNV_003 = new DefinicionDeError("CARP-CNV-003", "Para finalizar la conversación hay que indicar el resultado.");
        public static readonly DefinicionDeError CNV_004 = new DefinicionDeError("CARP-CNV-004", "Si el resultado es \"Sin respuesta\", el motivo y el detalle son obligatorios.");
        public static readonly DefinicionDeError CNV_005 = new DefinicionDeError("CARP-CNV-005", "El resultado, el motivo y el detalle solo se registran al finalizar la conversación.");
        public static readonly DefinicionDeError CNV_006 = new DefinicionDeError("CARP-CNV-006", "Una conversación solo pasa a Unida con la acción \"Unir conversaciones\".");
        public static readonly DefinicionDeError CNV_008 = new DefinicionDeError("CARP-CNV-008", "Solo la jefatura puede reasignar una conversación o dejarla sin asignar. (Ver 2.4.)");
        public static readonly DefinicionDeError CNV_009 = new DefinicionDeError("CARP-CNV-009", "Una conversación finalizada o unida no se puede modificar. Para cambiar una finalizada, reábrala primero.");
        public static readonly DefinicionDeError CNV_010 = new DefinicionDeError("CARP-CNV-010", "Al reabrir una conversación no se puede cambiar nada más en el mismo guardado: {columnas}. Reabra la conversación, guarde, y haga el otro cambio después.");
        public static readonly DefinicionDeError ARR_001 = new DefinicionDeError("CARP-ARR-001", "No hay una versión vigente de la plantilla. Publique una versión antes de registrar arreglos.");
        public static readonly DefinicionDeError ARR_002 = new DefinicionDeError("CARP-ARR-002", "Hay más de una versión vigente de la plantilla. Avise al equipo de soporte.");
        public static readonly DefinicionDeError ARR_003 = new DefinicionDeError("CARP-ARR-003", "El plazo debe estar entre 1 y {máximo} cuotas (la plantilla vigente admite {capacidad}).");
        public static readonly DefinicionDeError ARR_004 = new DefinicionDeError("CARP-ARR-004", "El monto abonado no puede ser mayor que el saldo inicial.");
        /// <summary>Cuando no hay saldo se usa <see cref="TextoSinSaldoDeArr005"/> (variante sin saldo; diseno/03 2.5 y 2.0.3).</summary>
        public static readonly DefinicionDeError ARR_005 = new DefinicionDeError("CARP-ARR-005", "Con una cuota menos ya se cubre el saldo remanente. El saldo se cubre con {n} cuotas.");

        /// <summary>Texto del <c>CARP-ARR-005</c> cuando el saldo remanente es cero (entre parentesis en la tabla de 2.5).</summary>
        public const string TextoSinSaldoDeArr005 = "El saldo remanente es cero; no hay cuotas que pagar.";

        public static readonly DefinicionDeError ARR_006 = new DefinicionDeError("CARP-ARR-006", "Faltan datos para calcular el arreglo: {columnas}.");
        public static readonly DefinicionDeError ARR_007 = new DefinicionDeError("CARP-ARR-007", "La cuota mensual debe ser mayor que cero.");
        public static readonly DefinicionDeError ARR_008 = new DefinicionDeError("CARP-ARR-008", "Un arreglo siempre se registra en estado Borrador.");
        public static readonly DefinicionDeError ARR_009 = new DefinicionDeError("CARP-ARR-009", "Estas columnas las gestiona el sistema y no se pueden modificar: {columnas}.");
        public static readonly DefinicionDeError ARR_010 = new DefinicionDeError("CARP-ARR-010", "Esa transición de estado no está permitida.");
        public static readonly DefinicionDeError ARR_011 = new DefinicionDeError("CARP-ARR-011", "El arreglo está congelado: no se puede modificar {columna}. Si el formato no se ha firmado, desbloquéelo; si ya está vigente, solo la jefatura puede reabrirlo.");
        public static readonly DefinicionDeError ARR_012 = new DefinicionDeError("CARP-ARR-012", "Esta acción no admite otros cambios en el mismo guardado: {columnas}.");
        public static readonly DefinicionDeError ARR_013 = new DefinicionDeError("CARP-ARR-013", "Para descartar el borrador hay que indicar el motivo.");
        public static readonly DefinicionDeError ARR_014 = new DefinicionDeError("CARP-ARR-014", "Para anular el arreglo hay que indicar el motivo.");
        public static readonly DefinicionDeError ARR_015 = new DefinicionDeError("CARP-ARR-015", "Para reabrir el arreglo hay que indicar el motivo.");
        public static readonly DefinicionDeError ARR_016 = new DefinicionDeError("CARP-ARR-016", "Solo la jefatura puede reabrir un arreglo.");
        public static readonly DefinicionDeError ARR_017 = new DefinicionDeError("CARP-ARR-017", "El arreglo no está congelado: no hay nada que desbloquear o reabrir.");
        public static readonly DefinicionDeError ARR_018 = new DefinicionDeError("CARP-ARR-018", "Un arreglo vigente no se desbloquea. Solo la jefatura puede reabrirlo.");
        public static readonly DefinicionDeError ARR_019 = new DefinicionDeError("CARP-ARR-019", "Para formalizar, primero hay que generar el formato para firma.");
        public static readonly DefinicionDeError ARR_020 = new DefinicionDeError("CARP-ARR-020", "Otro arreglo del mismo crédito se formalizó después de imprimir este formato. Desbloquee el borrador y vuelva a generar el formato.");
        public static readonly DefinicionDeError ARR_021 = new DefinicionDeError("CARP-ARR-021", "No se puede cambiar el calendario porque el arreglo ya tiene pagos registrados.");
        public static readonly DefinicionDeError ARR_022 = new DefinicionDeError("CARP-ARR-022", "Un arreglo terminado, anulado o descartado no se puede modificar. Un terminado solo se reabre por la jefatura.");
        public static readonly DefinicionDeError ARR_023 = new DefinicionDeError("CARP-ARR-023", "Este crédito alcanzó el máximo de 9.999 arreglos.");
        public static readonly DefinicionDeError ARR_024 = new DefinicionDeError("CARP-ARR-024", "El motivo solo se registra junto con su acción (descartar, anular o reabrir).");
        public static readonly DefinicionDeError CUO_001 = new DefinicionDeError("CARP-CUO-001", "En una cuota solo se puede registrar la fecha y el monto de pago. Columna no permitida: {columna}.");
        public static readonly DefinicionDeError CUO_002 = new DefinicionDeError("CARP-CUO-002", "Solo se pueden registrar pagos en un arreglo vigente.");
        public static readonly DefinicionDeError CUO_003 = new DefinicionDeError("CARP-CUO-003", "El pago de una cuota se registra con su fecha y su monto juntos, y el monto debe ser mayor que cero.");
        public static readonly DefinicionDeError VER_001 = new DefinicionDeError("CARP-VER-001", "Una versión de plantilla siempre se crea en estado Borrador.");
        public static readonly DefinicionDeError VER_002 = new DefinicionDeError("CARP-VER-002", "La huella, la fecha y el aprobador los fija el sistema al publicar.");
        public static readonly DefinicionDeError VER_003 = new DefinicionDeError("CARP-VER-003", "Solo una versión en Borrador se puede modificar. Para cambiar el formato cree una versión nueva.");
        public static readonly DefinicionDeError VER_004 = new DefinicionDeError("CARP-VER-004", "Esa transición de estado de la versión no está permitida. Solo se puede publicar un borrador.");
        public static readonly DefinicionDeError VER_005 = new DefinicionDeError("CARP-VER-005", "La versión no tiene archivo de plantilla cargado.");
        public static readonly DefinicionDeError VER_006 = new DefinicionDeError("CARP-VER-006", "El archivo de la plantilla no es un libro de Excel válido.");
        public static readonly DefinicionDeError VER_007 = new DefinicionDeError("CARP-VER-007", "El mapeo no se puede publicar: {errores}.");
        public static readonly DefinicionDeError VER_008 = new DefinicionDeError("CARP-VER-008", "La capacidad de la versión ({capacidad}) no coincide con las filas máximas del bloque de cuotas.");
        public static readonly DefinicionDeError VER_009 = new DefinicionDeError("CARP-VER-009", "La plantilla y el mapeo no producen un formato válido: {detalle}.");
        public static readonly DefinicionDeError VER_010 = new DefinicionDeError("CARP-VER-010", "Quedó más de una versión vigente. Avise al equipo de soporte.");
        public static readonly DefinicionDeError VER_012 = new DefinicionDeError("CARP-VER-012", "El número de versión debe tener el formato v07 (la letra v y 2 o 3 dígitos).");
        public static readonly DefinicionDeError VER_013 = new DefinicionDeError("CARP-VER-013", "Publicar no admite otros cambios en el mismo guardado.");
        public static readonly DefinicionDeError VER_014 = new DefinicionDeError("CARP-VER-014", "Una versión de plantilla no se activa ni se desactiva a mano: lo gestiona el sistema.");
        public static readonly DefinicionDeError MAP_001 = new DefinicionDeError("CARP-MAP-001", "Solo se puede modificar el mapeo de una versión en Borrador.");
        public static readonly DefinicionDeError MAP_002 = new DefinicionDeError("CARP-MAP-002", "La celda \"{celda}\" no está bien escrita. Use notación como B7.");
        public static readonly DefinicionDeError MAP_003 = new DefinicionDeError("CARP-MAP-003", "El campo \"{campo}\" no es compatible con el tipo de mapeo elegido.");
        public static readonly DefinicionDeError MAP_004 = new DefinicionDeError("CARP-MAP-004", "Las columnas del bloque de cuotas necesitan filas máximas entre 1 y 60.");
        public static readonly DefinicionDeError MAP_005 = new DefinicionDeError("CARP-MAP-005", "La hoja es obligatoria.");
        public static readonly DefinicionDeError MAP_006 = new DefinicionDeError("CARP-MAP-006", "Un mapeo no se puede mover a otra versión.");
        public static readonly DefinicionDeError MAP_007 = new DefinicionDeError("CARP-MAP-007", "Un mapeo de celda no se activa ni se desactiva a mano: lo gestiona el sistema.");
        public static readonly DefinicionDeError MAP_008 = new DefinicionDeError("CARP-MAP-008", "El nombre del mapeo lo calcula el sistema a partir de la hoja y la celda y no se puede cambiar a mano. Si necesita otro nombre, cambie la hoja o la celda.");
        public static readonly DefinicionDeError EML_001 = new DefinicionDeError("CARP-EML-001", "Las respuestas de una conversación salen siempre desde el buzón del área.");
        public static readonly DefinicionDeError EML_003 = new DefinicionDeError("CARP-EML-003", "Estas columnas las gestiona el sistema y no se pueden modificar.");
        public static readonly DefinicionDeError ENT_001 = new DefinicionDeError("CARP-ENT-001", "El correo no es entrante.");
        public static readonly DefinicionDeError ENT_002 = new DefinicionDeError("CARP-ENT-002", "No se encontró el correo.");
        public static readonly DefinicionDeError ENT_003 = new DefinicionDeError("CARP-ENT-003", "La cadena de conversaciones unidas no termina en una conversación activa.");
        public static readonly DefinicionDeError CFG_001 = new DefinicionDeError("CARP-CFG-001", "No se encontró la unidad de negocio del área, o hay más de una con ese nombre. Revise la variable de entorno sanic_carp_ev_nombrebu.");
        public static readonly DefinicionDeError CFG_002 = new DefinicionDeError("CARP-CFG-002", "No se encontró un único equipo propietario predeterminado en la unidad de negocio del área.");
        public static readonly DefinicionDeError CFG_003 = new DefinicionDeError("CARP-CFG-003", "No se encontró el equipo de jefatura en la unidad de negocio del área, o hay más de uno con ese nombre. Revise la variable de entorno sanic_carp_ev_nombreteamjefatura.");
        public static readonly DefinicionDeError CFG_004 = new DefinicionDeError("CARP-CFG-004", "No se encontró la cola del buzón del área. Revise la variable de entorno sanic_carp_ev_buzonarea.");
        public static readonly DefinicionDeError CFG_005 = new DefinicionDeError("CARP-CFG-005", "Hay más de una cola con la dirección del buzón del área. Revise la variable de entorno sanic_carp_ev_buzonarea y las colas del entorno.");
        public static readonly DefinicionDeError CFG_006 = new DefinicionDeError("CARP-CFG-006", "La variable de entorno {variable} no tiene valor.");

        public static IReadOnlyList<DefinicionDeError> Todos { get; } = Array.AsReadOnly(new DefinicionDeError[]
        {
            GEN_001,
            GEN_002,
            GEN_003,
            GEN_004,
            GEN_005,
            GEN_006,
            GEN_007,
            GEN_008,
            GEN_009,
            GEN_010,
            GEN_011,
            GEN_900,
            REC_001,
            REC_002,
            REC_003,
            REC_900,
            TOM_001,
            TOM_002,
            TOM_003,
            TOM_004,
            TOM_900,
            UNI_001,
            UNI_002,
            UNI_003,
            UNI_004,
            UNI_005,
            UNI_006,
            UNI_007,
            UNI_008,
            UNI_009,
            UNI_010,
            UNI_900,
            INI_001,
            INI_002,
            INI_003,
            INI_004,
            INI_005,
            INI_006,
            INI_900,
            PRO_001,
            CLI_001,
            CLI_002,
            CLI_003,
            CLI_004,
            CLI_005,
            CNV_001,
            CNV_002,
            CNV_003,
            CNV_004,
            CNV_005,
            CNV_006,
            CNV_008,
            CNV_009,
            CNV_010,
            ARR_001,
            ARR_002,
            ARR_003,
            ARR_004,
            ARR_005,
            ARR_006,
            ARR_007,
            ARR_008,
            ARR_009,
            ARR_010,
            ARR_011,
            ARR_012,
            ARR_013,
            ARR_014,
            ARR_015,
            ARR_016,
            ARR_017,
            ARR_018,
            ARR_019,
            ARR_020,
            ARR_021,
            ARR_022,
            ARR_023,
            ARR_024,
            CUO_001,
            CUO_002,
            CUO_003,
            VER_001,
            VER_002,
            VER_003,
            VER_004,
            VER_005,
            VER_006,
            VER_007,
            VER_008,
            VER_009,
            VER_010,
            VER_012,
            VER_013,
            VER_014,
            MAP_001,
            MAP_002,
            MAP_003,
            MAP_004,
            MAP_005,
            MAP_006,
            MAP_007,
            MAP_008,
            EML_001,
            EML_003,
            ENT_001,
            ENT_002,
            ENT_003,
            CFG_001,
            CFG_002,
            CFG_003,
            CFG_004,
            CFG_005,
            CFG_006,
        });

        /// <summary>La definicion de un codigo, o <c>null</c> si el documento no lo tiene.</summary>
        public static DefinicionDeError Buscar(string codigo)
        {
            foreach (var d in Todos)
            {
                if (string.Equals(d.Codigo, codigo, StringComparison.Ordinal)) return d;
            }
            return null;
        }
    }
}
