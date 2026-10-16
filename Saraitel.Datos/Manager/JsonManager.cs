using System;
using System.IO;
using System.Text.Json;
using Saraitel.Datos.Models;

namespace Saraitel.Datos.Managers
{
    public class JsonManager
    {
        private static JsonManager? _instance;
        private readonly string _filePath;

        private JsonManager()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string rutaProyecto = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\.."));
            _filePath = Path.Combine(rutaProyecto,"Saraitel.Datos","Datos","datos.json");
            VerificarArchivo();
        }

        public static JsonManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new JsonManager();
                }
                return _instance;
            }
        }

        private void VerificarArchivo()
        {
            string carpeta = Path.GetDirectoryName(_filePath)!;

            if (!Directory.Exists(carpeta))
            {
                Directory.CreateDirectory(carpeta);
            }

            if (!File.Exists(_filePath))
            {
                GuardarDatos(new DataStore());
            }
        }

        public DataStore LeerDatos()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    Console.WriteLine("NO EXISTE EL ARCHIVO JSON");
                    return new DataStore();
                }
                string json = File.ReadAllText(_filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    Console.WriteLine("JSON VACÍO");
                    return new DataStore();
                }
                var opciones = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNameCaseInsensitive = true
                };
                var data = JsonSerializer.Deserialize<DataStore>(json, opciones);
                return data ?? new DataStore();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR JSON: {ex}");
                return new DataStore();
            }
        }

        public void GuardarDatos(DataStore data)
        {
            var opciones = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(data, opciones);
            File.WriteAllText(_filePath, json);
        }
    }
}