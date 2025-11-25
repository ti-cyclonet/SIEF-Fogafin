namespace InscripcionEntidades.Config
{
    public static class CloudConfig
    {
        // 🔐 Código de autorización centralizado para consumos en nube
        public static string GetAuthorizationCode()
        {
            return Environment.GetEnvironmentVariable("CLOUD_AUTH_CODE") ?? "dev-12345";
        }

        // 📧 Configuración de Email API
        public static string GetEmailApiBaseUrl()
        {
            return Environment.GetEnvironmentVariable("EMAIL_API_BASE_URL") 
                ?? "https://fn-email-corp-dev-eus2-h5cjfbeud6h7axab.eastus2-01.azurewebsites.net";
        }

        public static string GetEmailApiKey()
        {
            return GetAuthorizationCode();
        }

        // 💾 Configuración de SQL
        public static string GetSqlConnectionString()
        {
            return Environment.GetEnvironmentVariable("SqlConnectionString") 
                ?? throw new InvalidOperationException("SqlConnectionString no configurada");
        }

        // ☁️ Configuración de Storage
        public static string GetStorageConnectionString()
        {
            return Environment.GetEnvironmentVariable("StorageConnectionString") 
                ?? throw new InvalidOperationException("StorageConnectionString no configurada");
        }

        // 📦 Contenedores de Blob Storage
        public static string GetAdjuntosContainer() => "adjuntos";
        public static string GetResumenesContainer() => "resumenes";
    }
}
