/**
 * Utilidad para obtener el usuario de Windows desde el frontend
 */
class WindowsAuthHelper {
    
    static async obtenerUsuarioWindows() {
        try {
            // Método 1: Desde localStorage (persistencia)
            const usuarioGuardado = localStorage.getItem('detectedWindowsUser');
            if (usuarioGuardado) {
                return usuarioGuardado;
            }

            // Método 2: Desde variables del navegador
            const usuarioNavegador = this.obtenerDesdeNavegador();
            if (usuarioNavegador) {
                localStorage.setItem('detectedWindowsUser', usuarioNavegador);
                return usuarioNavegador;
            }

            // Método 3: ActiveX (IE/Edge legacy)
            const usuarioActiveX = await this.obtenerConActiveX();
            if (usuarioActiveX) {
                localStorage.setItem('detectedWindowsUser', usuarioActiveX);
                return usuarioActiveX;
            }

            // Método 4: Desde URL o parámetros
            const usuarioURL = this.obtenerDesdeURL();
            if (usuarioURL) {
                localStorage.setItem('detectedWindowsUser', usuarioURL);
                return usuarioURL;
            }

            // Método 5: Prompt al usuario (solo una vez)
            const usuarioPrompt = await this.obtenerConPrompt();
            if (usuarioPrompt) {
                localStorage.setItem('detectedWindowsUser', usuarioPrompt);
                return usuarioPrompt;
            }

            return null;
            
        } catch (error) {
            console.warn('Error obteniendo usuario:', error);
            return null;
        }
    }

    static async obtenerConActiveX() {
        try {
            if (typeof ActiveXObject !== 'undefined') {
                const network = new ActiveXObject("WScript.Network");
                return network.UserName;
            }
            return null;
        } catch (error) {
            return null;
        }
    }

    static obtenerDesdeNavegador() {
        try {
            // Intentar obtener desde el user agent o variables del sistema
            const userAgent = navigator.userAgent;
            const platform = navigator.platform;
            
            // Verificar si hay información del usuario en el navegador
            if (window.external && window.external.ConnectToConnectionPoint) {
                // Método específico para algunos navegadores corporativos
                return null;
            }
            
            // Verificar variables de entorno del navegador (si están disponibles)
            if (typeof process !== 'undefined' && process.env) {
                return process.env.USERNAME || process.env.USER;
            }
            
            return null;
        } catch (error) {
            return null;
        }
    }

    static obtenerDesdeURL() {
        const params = new URLSearchParams(window.location.search);
        return params.get('user') || params.get('username');
    }

    static async obtenerConPrompt() {
        // Solo preguntar si no se ha preguntado antes
        const yaPreguntoUsuario = localStorage.getItem('userPromptShown');
        if (yaPreguntoUsuario) {
            return null;
        }

        try {
            if (typeof Swal !== 'undefined') {
                const { value: usuario } = await Swal.fire({
                    title: 'Configuración Inicial',
                    input: 'text',
                    inputLabel: 'Ingrese su usuario de Windows para acceso automático:',
                    showCancelButton: false,
                    allowOutsideClick: false,
                    confirmButtonText: 'Continuar'
                });
                localStorage.setItem('userPromptShown', 'true');
                
                if (usuario && usuario.trim() && usuario.trim() !== '') {
                    return usuario.trim().toLowerCase();
                }
            }
            return null;
        } catch (error) {
            return null;
        }
    }

    static async obtenerDesdeBackend() {
        try {
            // Intentar obtener usuario detectado localmente
            const usuarioLocal = localStorage.getItem('detectedWindowsUser');
            
            let params = {};
            let headers = { 'Accept': 'text/plain' };
            
            // Si hay usuario local, enviarlo al backend
            if (usuarioLocal) {
                params.user = usuarioLocal;
                headers['X-User-Identity'] = usuarioLocal;
            }
            
            // Usar getApiUrl del config.js si está disponible
            const url = typeof getApiUrl === 'function' 
                ? getApiUrl('whoami', params)
                : '/api/whoami';
            
            const response = await fetch(url, {
                method: 'GET',
                credentials: 'include',
                headers: headers
            });
            
            if (response.ok) {
                const usuario = await response.text();
                return usuario.trim();
            }
            return null;
        } catch (error) {
            return null;
        }
    }

    static async iniciarSesionAutomatica() {
        try {
            let usuario = await this.obtenerUsuarioWindows();
            
            if (usuario) {
                usuario = usuario.toLowerCase().trim();
                if (usuario.includes('\\')) {
                    usuario = usuario.split('\\').pop();
                }
                if (usuario.includes('@')) {
                    usuario = usuario.split('@')[0];
                }
            }

            return usuario || null;
            
        } catch (error) {
            console.error('Error en inicio automático:', error);
            return null;
        }
    }
}

window.WindowsAuthHelper = WindowsAuthHelper;