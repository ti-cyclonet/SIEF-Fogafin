// 🔐 CONFIGURACIÓN CENTRALIZADA DE AUTORIZACIÓN EN NUBE
const CLOUD_CONFIG = {
  AUTH_CODE: window.AZURE_FUNCTION_KEY || getAuthCode(),
  API_BASE_URL: isLocalEnvironment()
    ? "http://localhost:7176/api/"
    : "https://rg-funciones-inscripcion-d0dpgpfrcmh8gxdg.eastus2-01.azurewebsites.net/api/"
};

// Función para obtener código de autorización
function getAuthCode() {
  // Intentar desde localStorage primero
  const storedKey = localStorage.getItem('azureFunctionKey');
  if (storedKey) return storedKey;
  
  // Código base64 ofuscado (configurar en producción)
  const encoded = "YOUR_ENCODED_AZURE_FUNCTION_KEY_HERE";
  

  try {
    return atob(encoded);
  } catch (e) {
    return "";
  }
}

// Función para detectar si es ambiente local
function isLocalEnvironment() {
  const hostname = window.location.hostname;
  return hostname === "localhost" || 
         hostname === "127.0.0.1" || 
         hostname.startsWith("192.168.") || 
         hostname.startsWith("10.") || 
         hostname.includes(".local");
}

const getApiUrl = (endpoint, params = {}) => {
  const isLocal = isLocalEnvironment();
  const baseUrl = `${CLOUD_CONFIG.API_BASE_URL}${endpoint}`;
  
  if (isLocal) {
    // En local, solo agregar parámetros si existen
    const queryString = Object.keys(params).length > 0 
      ? '?' + new URLSearchParams(params).toString() 
      : '';
    return baseUrl + queryString;
  } else {
    // En nube, agregar code y otros parámetros
    if (!CLOUD_CONFIG.AUTH_CODE) {
      console.warn('⚠️ AUTH_CODE no configurado para ambiente de nube');
    }
    const allParams = { code: CLOUD_CONFIG.AUTH_CODE, ...params };
    return `${baseUrl}?${new URLSearchParams(allParams).toString()}`;
  }
};
