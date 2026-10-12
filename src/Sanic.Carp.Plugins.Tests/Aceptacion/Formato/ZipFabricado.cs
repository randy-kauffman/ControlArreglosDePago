using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Formato
{
    /// <summary>Una entrada del ZIP tal como se escribe a mano: los campos del encabezado se pueden falsear.</summary>
    internal sealed class EntradaZip
    {
        public string Nombre;
        /// <summary>Los bytes tal como van en el archivo (ya "comprimidos" según <see cref="Metodo"/>).</summary>
        public byte[] Datos;
        /// <summary>0 = almacenada (sin comprimir), 8 = deflate. Otro valor = método que ningún lector admite.</summary>
        public ushort Metodo;
        /// <summary>Tamaño descomprimido que declaran el encabezado local y el directorio central. Nulo = el real (los bytes de Datos).</summary>
        public uint? TamanoDeclarado;
        /// <summary>Tamaño comprimido que declaran los encabezados. Nulo = Datos.Length.</summary>
        public uint? TamanoComprimidoDeclarado;
        /// <summary>Posición del encabezado local que dice el directorio central. Nulo = la verdadera.</summary>
        public uint? PosicionLocalDeclarada;
    }

    /// <summary>
    /// Escribe un ZIP a mano. <see cref="ZipArchive"/> no deja falsear los tamaños declarados, y esa es la pieza que hace falta para
    /// probar que el tope de tamaño se mide sobre los bytes realmente leídos y no sobre lo que dice el directorio central
    /// (diseno/03 4.3, punto 11 del 2026-10-12). Una entrada ALMACENADA (método 0) con un tamaño descomprimido falso se lee completa
    /// en .NET (la BCL solo recorta las entradas deflate al tamaño declarado), así que la prueba se ve igual en net8.0 y en net462.
    /// </summary>
    internal static class ZipFabricado
    {
        private static readonly uint[] TablaCrc = CrearTablaCrc();

        public static List<EntradaZip> DeXlsx(byte[] xlsx)
        {
            var lista = new List<EntradaZip>();
            using (var zip = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read))
            {
                foreach (var e in zip.Entries)
                {
                    using (var s = e.Open())
                    using (var ms = new MemoryStream())
                    {
                        s.CopyTo(ms);
                        lista.Add(new EntradaZip { Nombre = e.FullName, Datos = ms.ToArray() });
                    }
                }
            }
            return lista;
        }

        /// <summary>El .xlsx con sus entradas reescritas a mano (almacenadas, con tamaños honestos) y luego ajustadas por <paramref name="ajustar"/>.</summary>
        public static byte[] Con(byte[] xlsx, Action<List<EntradaZip>> ajustar)
        {
            var entradas = DeXlsx(xlsx);
            ajustar(entradas);
            return Construir(entradas);
        }

        public static EntradaZip Buscar(List<EntradaZip> entradas, string nombre)
        {
            var e = entradas.Find(x => x.Nombre == nombre);
            if (e == null) throw new InvalidOperationException("la prueba busca la parte '" + nombre + "' y no está en la plantilla");
            return e;
        }

        public static byte[] Construir(IList<EntradaZip> entradas)
        {
            var ms = new MemoryStream();
            var posiciones = new List<long>();
            foreach (var e in entradas)
            {
                posiciones.Add(ms.Position);
                var nombre = Encoding.UTF8.GetBytes(e.Nombre);
                W32(ms, 0x04034B50); W16(ms, 20); W16(ms, 0); W16(ms, e.Metodo); W16(ms, 0); W16(ms, 0x21);
                W32(ms, CalcularCrc(e.Datos)); W32(ms, e.TamanoComprimidoDeclarado ?? (uint)e.Datos.Length);
                W32(ms, e.TamanoDeclarado ?? (uint)e.Datos.Length); W16(ms, nombre.Length); W16(ms, 0);
                ms.Write(nombre, 0, nombre.Length);
                ms.Write(e.Datos, 0, e.Datos.Length);
            }
            long inicioDirectorio = ms.Position;
            for (int i = 0; i < entradas.Count; i++)
            {
                var e = entradas[i];
                var nombre = Encoding.UTF8.GetBytes(e.Nombre);
                W32(ms, 0x02014B50); W16(ms, 20); W16(ms, 20); W16(ms, 0); W16(ms, e.Metodo); W16(ms, 0); W16(ms, 0x21);
                W32(ms, CalcularCrc(e.Datos)); W32(ms, e.TamanoComprimidoDeclarado ?? (uint)e.Datos.Length);
                W32(ms, e.TamanoDeclarado ?? (uint)e.Datos.Length); W16(ms, nombre.Length); W16(ms, 0); W16(ms, 0); W16(ms, 0); W16(ms, 0);
                W32(ms, 0); W32(ms, e.PosicionLocalDeclarada ?? (uint)posiciones[i]);
                ms.Write(nombre, 0, nombre.Length);
            }
            long tamanoDirectorio = ms.Position - inicioDirectorio;
            W32(ms, 0x06054B50); W16(ms, 0); W16(ms, 0); W16(ms, entradas.Count); W16(ms, entradas.Count);
            W32(ms, (uint)tamanoDirectorio); W32(ms, (uint)inicioDirectorio); W16(ms, 0);
            return ms.ToArray();
        }

        private static void W16(Stream s, int v)
        {
            s.WriteByte((byte)v);
            s.WriteByte((byte)(v >> 8));
        }

        private static void W32(Stream s, uint v)
        {
            W16(s, (int)(v & 0xFFFF));
            W16(s, (int)(v >> 16));
        }

        private static uint CalcularCrc(byte[] datos)
        {
            uint crc = 0xFFFFFFFF;
            foreach (var b in datos) crc = TablaCrc[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return ~crc;
        }

        private static uint[] CrearTablaCrc()
        {
            var tabla = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                tabla[n] = c;
            }
            return tabla;
        }
    }
}
