using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Sanic.Carp.Plugins.Dominio.Puertos;
using Sanic.Carp.Plugins.Formato;

namespace Sanic.Carp.Plugins.Tests.Dobles
{
    /// <summary>
    /// <see cref="IGeneradorDeFormato"/> para las pruebas de la Custom API (diseno/03 4.5): devuelve bytes fijos o lanza la excepción
    /// pedida, y guarda lo que recibió. No genera nada: ningún archivo sale de esta clase salvo los bytes que se le configuren.
    /// </summary>
    internal sealed class GeneradorFalso : IGeneradorDeFormato
    {
        /// <summary>
        /// Bytes por defecto del archivo: <c>{ 0x50, 0x4B, 0x03, 0x04, 0x46, 0x41, 0x4C, 0x53, 0x4F }</c> ("PK\3\4FALSO").
        /// <b>VUELTA 2 (hallazgo 8):</b> hoy es un <c>byte[]</c> estático que se devuelve por referencia a todas las instancias (mutar el resultado contamina a las demás). Debe pasar a
        /// una PROPIEDAD estática que devuelva una copia nueva en cada lectura (el código que la lee sigue compilando) y cada instancia y cada resultado de <c>Generar</c> debe llevar
        /// su propia copia: <c>PA_STA_03</c> y <c>PA_STA_04</c> (<c>EstadoEstaticoCompartidoAceptacion</c>).
        /// </summary>
        public static readonly byte[] ArchivoPorDefecto = { 0x50, 0x4B, 0x03, 0x04, 0x46, 0x41, 0x4C, 0x53, 0x4F };

        /// <summary>Devuelve <see cref="ArchivoPorDefecto"/>, sin advertencias, sin recálculo y sin excepción.</summary>
        private byte[] _archivo = ArchivoPorDefecto;
        private IReadOnlyList<string> _advertencias = new string[0];

        public GeneradorFalso() { }

        /// <summary>Los bytes que devuelve <see cref="Generar"/> (por defecto <see cref="ArchivoPorDefecto"/>).</summary>
        public byte[] Archivo { get { return _archivo; } set { _archivo = value; } }

        public IReadOnlyList<string> Advertencias { get { return _advertencias; } set { _advertencias = value; } }

        public bool MarcadoRecalculoAlAbrir { get; set; }

        /// <summary>Si no es nula, <see cref="Generar"/> la lanza (la MISMA instancia; típicamente un subtipo de <c>FormatoException</c>) después de guardar lo recibido.</summary>
        public Exception Excepcion { get; set; }

        /// <summary>Si no es nula, es lo que devuelve <see cref="CalcularHuella"/>; si es nula se calcula SHA-256 hexadecimal minúscula con la BCL (nunca con el generador real).</summary>
        public string HuellaFija { get; set; }

        // ---- Lo recibido en la ÚLTIMA llamada a Generar (referencias, no copias) ----
        public int LlamadasAGenerar { get; private set; }
        public byte[] UltimaPlantilla { get; private set; }
        public string UltimaHuellaEsperada { get; private set; }
        public IReadOnlyList<MapeoCelda> UltimoMapeo { get; private set; }
        public DatosArreglo UltimosDatos { get; private set; }

        /// <summary>Cuántas veces se llamó a <see cref="CalcularHuella"/>.</summary>
        public int LlamadasACalcularHuella { get; private set; }

        public ResultadoGeneracion Generar(byte[] plantilla, string huellaEsperada, IReadOnlyList<MapeoCelda> mapeo, DatosArreglo datos)
        {
            LlamadasAGenerar++;
            UltimaPlantilla = plantilla;
            UltimaHuellaEsperada = huellaEsperada;
            UltimoMapeo = mapeo;
            UltimosDatos = datos;
            if (Excepcion != null) throw Excepcion;
            return new ResultadoGeneracion(Archivo, Advertencias, MarcadoRecalculoAlAbrir);
        }

        public string CalcularHuella(byte[] contenido)
        {
            LlamadasACalcularHuella++;
            if (HuellaFija != null) return HuellaFija;
            // SHA-256 de la BCL, no del generador real: el esperado de una prueba no puede salir del código bajo prueba (4.3 punto 9).
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(contenido);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }
    }
}
