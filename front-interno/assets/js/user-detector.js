/**
 * Detector de usuario para entornos en la nube
 */
class UserDetector {
    
    static init() {
        // Verificar si hay usuario en la URL al cargar la página
        this.checkURLUser();
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
    

    
    static async configureUser() {
        const currentUser = localStorage.getItem('detectedWindowsUser') || '';
        
        const { value: newUser } = await Swal.fire({
            title: 'Configurar Usuario Automático',
            input: 'text',
            inputLabel: 'Ingrese su usuario de Windows para acceso automático:',
            inputPlaceholder: 'ej: AlfredoMamby',
            inputValue: currentUser,
            showCancelButton: true,
            confirmButtonText: 'Configurar',
            cancelButtonText: 'Cancelar'
        });
        
        if (newUser && newUser.trim()) {
            const cleanUser = newUser.trim().toLowerCase();
            localStorage.setItem('detectedWindowsUser', cleanUser);
            localStorage.removeItem('userPromptShown');
            
            Swal.fire({
                icon: 'success',
                title: 'Usuario Configurado',
                text: `Usuario ${cleanUser} configurado correctamente`,
                timer: 2000,
                showConfirmButton: false
            });
            
            // Actualizar el campo de usuario y etiqueta si existen
            const inputUsuario = document.getElementById('inputUsuario');
            const usuarioLabel = document.getElementById('usuarioLabel');
            if (inputUsuario) {
                inputUsuario.value = cleanUser;
            }
            if (usuarioLabel) {
                usuarioLabel.textContent = cleanUser;
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