// Utilidad para autenticación integrada de Windows en intranet
class WindowsAuth {
  static async obtenerUsuarioWindows() {
    try {
      const URL_WHOAMI = getApiUrl("whoami");
      
      const response = await fetch(URL_WHOAMI, {
        method: 'GET',
        credentials: 'include',
        headers: {
          'X-User-Identity': this.obtenerUsuarioLocal()
        }
      });
      
      if (response.ok) {
        const usuario = await response.text();
        return usuario.trim();
      }
    } catch (error) {
      console.warn('Error obteniendo usuario Windows:', error);
    }
    
    return 'adminSief';
  }

  static obtenerUsuarioLocal() {
    try {
      if (window.ActiveXObject || "ActiveXObject" in window) {
        const network = new ActiveXObject("WScript.Network");
        return network.UserName;
      }
    } catch (e) {}
    
    const params = new URLSearchParams(window.location.search);
    return params.get('user') || params.get('usuario') || '';
  }

  static async verificarSesionActiva() {
    const usuario = await this.obtenerUsuarioWindows();
    return usuario && usuario !== 'adminSief';
  }
}