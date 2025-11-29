// 🔧 CONFIGURACIÓN CENTRALIZADA - PLANTILLA
// Copiar este archivo como config.js y configurar con valores reales

const CLOUD_CONFIG = {
  AUTH_CODE: process.env.AUTH_CODE || "TU_AUTH_CODE_AQUI",
  API_BASE_URL: window.location.hostname === "localhost"
    ? "http://localhost:7176/api"
    : process.env.API_BASE_URL || "https://tu-api-url.azurewebsites.net/api"
};

const getApiUrl = (endpoint) => {
  const isLocal = window.location.hostname === "localhost";
  const url = `${CLOUD_CONFIG.API_BASE_URL}/${endpoint}`;
  return isLocal ? url : `${url}?code=${CLOUD_CONFIG.AUTH_CODE}`;
};