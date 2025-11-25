// 🔐 CONFIGURACIÓN CENTRALIZADA DE AUTORIZACIÓN EN NUBE
const CLOUD_CONFIG = {
  AUTH_CODE: "TU_AUTH_CODE_AQUI",
  API_BASE_URL: window.location.hostname === "localhost"
    ? "http://localhost:7176/api"
    : "TU_API_URL_AQUI"
};

const getApiUrl = (endpoint) => {
  const isLocal = window.location.hostname === "localhost";
  const url = `${CLOUD_CONFIG.API_BASE_URL}/${endpoint}`;
  return isLocal ? url : `${url}?code=${CLOUD_CONFIG.AUTH_CODE}`;
};