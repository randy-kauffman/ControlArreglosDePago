using System;
using System.Collections.Generic;
using System.Globalization;
using Sanic.Carp.Plugins.Dominio;

namespace Sanic.Carp.Plugins.Formato
{
    /// <summary>
    /// Datos sintéticos para la generación de prueba que se hace al publicar una versión de plantilla (DC-19; diseno/03 2.8,
    /// <c>VersionPlantillaActualizarStep</c> paso 3): un <see cref="DatosArreglo"/> con plazo igual a la capacidad y TODAS las claves
    /// del generador con valores inventados, para que cualquier celda mapeada tenga algo que imprimir.
    /// <para>
    /// Nunca son datos reales (def. L411, diseno/03 4.5): cada texto que parece un dato personal o de cuenta (oficial, nombre, cédula,
    /// correo, celular, número de producto, cuenta a debitar y comentarios) lleva la palabra <c>PRUEBA</c> y el correo usa el dominio
    /// reservado <c>example.com</c> (RFC 2606). Son deterministas: no leen el reloj ni números aleatorios (diseno/03 0.6).
    /// </para>
    /// </summary>
    internal static class DatosDePrueba
    {
        /// <summary>
        /// Datos de prueba para una plantilla de <paramref name="capacidad"/> cuotas. Una capacidad fuera de 1..<see cref="Campos.CapacidadMaxima"/>
        /// lanza <see cref="ArgumentOutOfRangeException"/> (la publicación ya la rechazó con <c>CARP-VER-007</c> o <c>-008</c>).
        /// </summary>
        public static DatosArreglo Crear(int capacidad)
        {
            if (capacidad < 1 || capacidad > Campos.CapacidadMaxima)
            {
                throw new ArgumentOutOfRangeException(nameof(capacidad), capacidad,
                    "La capacidad de la generación de prueba debe estar entre 1 y " + Campos.CapacidadMaxima.ToString(CultureInfo.InvariantCulture) + ".");
            }

            // Todo fijo: sin reloj ni números aleatorios (diseno/03 0.6). Fechas solo-fecha con día 15, que existe en todos los meses.
            var formalizacion = new DateTime(2026, 1, 15);
            var inicio = new DateTime(2026, 2, 15);
            decimal cuota = 1234.56m;

            var cuotas = new List<CuotaDato>(capacidad);
            // La primera mitad (redondeando hacia arriba) trae pago completo y el resto ninguno: con una sola cuota queda pagada
            // (hay al menos un pago completo) y con más de una siempre queda alguna sin pago (diseno/03 4.3, punto 8).
            int pagadas = (capacidad + 1) / 2;
            for (int i = 1; i <= capacidad; i++)
            {
                var vencimiento = inicio.AddMonths(i - 1);
                if (i <= pagadas) cuotas.Add(new CuotaDato(i, vencimiento, cuota, vencimiento, cuota));
                else cuotas.Add(new CuotaDato(i, vencimiento, cuota));
            }

            // Una clave por cada opción de valor simple de diseno/03 1.1 (las 20, con moneda); ninguna en nulo.
            var valores = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "fechaFormalizacion", formalizacion },
                { "oficial", "OFICIAL DE PRUEBA" },
                { "clienteNombre", "CLIENTE DE PRUEBA" },
                { "clienteCedula", "PRUEBA 0000000000000" },
                { "clienteCorreo", "prueba@example.com" },
                { "clienteCelular", "PRUEBA 00000000" },
                { "numeroProducto", "PRUEBA-0000001" },
                { "tipo", ClavesDelGenerador.TextoFormal(TipoDeArreglo.Temporal) },
                { "numeroArreglo", 1 },
                { "saldoInicial", 250000.75m },
                { "montoAbonado", 10000.25m },
                { "saldoRemanente", 240000.50m },
                { "plazo", capacidad },
                { "cuota", cuota },
                { "totalPagar", cuota * capacidad },
                { "fechaInicio", inicio },
                { "fechaFin", cuotas[capacidad - 1].Fecha },
                { "cuentaDebitar", "CUENTA PRUEBA 0000000000" },
                { "comentarios", "COMENTARIO DE PRUEBA" },
                { "moneda", ClavesDelGenerador.TextoFormal(Moneda.Cordobas) }
            };

            return new DatosArreglo(valores, capacidad, cuotas);
        }
    }
}
