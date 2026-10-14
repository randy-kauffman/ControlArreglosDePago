using System;
using System.Collections.Generic;
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
        /// <summary>Bytes por defecto del archivo: <c>{ 0x50, 0x4B, 0x03, 0x04, 0x46, 0x41, 0x4C, 0x53, 0x4F }</c> ("PK\3\4FALSO").</summary>
        public static readonly byte[] ArchivoPorDefecto = { 0x50, 0x4B, 0x03, 0x04, 0x46, 0x41, 0x4C, 0x53, 0x4F };

        /// <summary>Devuelve <see cref="ArchivoPorDefecto"/>, sin advertencias, sin recálculo y sin excepción.</summary>
        public GeneradorFalso() { throw new NotImplementedException(); }

        /// <summary>Los bytes que devuelve <see cref="Generar"/> (por defecto <see cref="ArchivoPorDefecto"/>).</summary>
        public byte[] Archivo { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }

        public IReadOnlyList<string> Advertencias { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }

        public bool MarcadoRecalculoAlAbrir { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }

        /// <summary>Si no es nula, <see cref="Generar"/> la lanza (la MISMA instancia; típicamente un subtipo de <c>FormatoException</c>) después de guardar lo recibido.</summary>
        public Exception Excepcion { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }

        /// <summary>Si no es nula, es lo que devuelve <see cref="CalcularHuella"/>; si es nula se calcula SHA-256 hexadecimal minúscula con la BCL (nunca con el generador real).</summary>
        public string HuellaFija { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }

        // ---- Lo recibido en la ÚLTIMA llamada a Generar (referencias, no copias) ----
        public int LlamadasAGenerar { get { throw new NotImplementedException(); } }
        public byte[] UltimaPlantilla { get { throw new NotImplementedException(); } }
        public string UltimaHuellaEsperada { get { throw new NotImplementedException(); } }
        public IReadOnlyList<MapeoCelda> UltimoMapeo { get { throw new NotImplementedException(); } }
        public DatosArreglo UltimosDatos { get { throw new NotImplementedException(); } }

        /// <summary>Cuántas veces se llamó a <see cref="CalcularHuella"/>.</summary>
        public int LlamadasACalcularHuella { get { throw new NotImplementedException(); } }

        public ResultadoGeneracion Generar(byte[] plantilla, string huellaEsperada, IReadOnlyList<MapeoCelda> mapeo, DatosArreglo datos) { throw new NotImplementedException(); }

        public string CalcularHuella(byte[] contenido) { throw new NotImplementedException(); }
    }
}
