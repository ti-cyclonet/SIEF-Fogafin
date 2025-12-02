/**
 * Detector de usuario para entornos en la nube
 */
class UserDetector {
    
    static init() {
        // Verificar si hay usuario en la URL al cargar la página
        this.checkURLUser();
        
        // Agregar botón para configurar usuario manualmente
        this.addUserConfigButton();
    }
    
    static checkURLUser() {
        const params = new URLSearchParams(window.location.search);
        const userFromURL = params.get('user') || params.get('username');
        
        if (userFromURL) {
            localStorage.setItem('detectedWindowsUser', userFromURL.toLowerCase());
            console.log('Usuario detectado desde URL:', userFromURL);
            
            // Limpiar URL sin recargar la página
            const url = new URL(window.location);
            url.searchParams.delete('user');
            url.searchParams.delete('username');
            window.history.replaceState({}, document.title, url.pathname + url.search);
        }
    }
    
    static addUserConfigButton() {
        // Solo agregar en la página de login
        if (window.location.pathname.includes('index.html') || window.location.pathname.endsWith('/interno/')) {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'btn btn-link btn-sm mt-2';
            button.innerHTML = '<i class="fas fa-user-cog"></i> Configurar usuario automático';
            button.onclick = this.configureUser;
            
            // Insertar después del formulario de login
            const form = document.getElementById('loginForm');
            if (form) {
                form.parentNode.insertBefore(button, form.nextSibling);
            }
        }
    }
    
    static configureUser() {
        const currentUser = localStorage.getItem('detectedWindowsUser') || '';
        const newUser = prompt('Ingrese su usuario de Windows para acceso automático:', currentUser);
        
        if (newUser && newUser.trim()) {
            localStorage.setItem('detectedWindowsUser', newUser.trim().toLowerCase());
            localStorage.removeItem('userPromptShown'); // Permitir que se use el nuevo usuario
            
            alert('Usuario configurado correctamente. La próxima vez se usará automáticamente.');
            
            // Actualizar el campo de usuario si existe
            const inputUsuario = document.getElementById('inputUsuario');
            if (inputUsuario) {
                inputUsuario.value = newUser.trim().toLowerCase();
            }
        }
    }
    
    static clearStoredUser() {
        localStorage.removeItem('detectedWindowsUser');
        localStorage.removeItem('userPromptShown');
        console.log('Usuario almacenado eliminado');
    }
}

// Auto-inicializar cuando se carga el DOM
document.addEventListener('DOMContentLoaded', () => {
    UserDetector.init();
});

// Exportar para uso global
window.UserDetector = UserDetector;