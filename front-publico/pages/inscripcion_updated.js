function showFileError(fieldId, message) {
  const wrapper = document.getElementById(fieldId + 'Wrapper');
  const container = wrapper.closest('.col-md-8');
  const existingError = container.querySelector('.file-error');
  if (existingError) existingError.remove();
  if (wrapper) {
    wrapper.style.setProperty('border', '1px solid #dc3545', 'important');
    wrapper.style.setProperty('border-color', '#dc3545', 'important');
  }
  const errorDiv = document.createElement('div');
  errorDiv.className = 'file-error';
  errorDiv.style.color = '#dc3545';
  errorDiv.style.fontSize = '0.875rem';
  errorDiv.style.marginTop = '0.5rem';
  errorDiv.style.display = 'block';
  errorDiv.textContent = message;
  container.appendChild(errorDiv);
}

function clearFileError(fieldId) {
  const wrapper = document.getElementById(fieldId + 'Wrapper');
  const container = wrapper.closest('.col-md-8');
  const existingError = container.querySelector('.file-error');
  if (existingError) existingError.remove();
  if (wrapper) {
    wrapper.style.removeProperty('border');
    wrapper.style.removeProperty('border-color');
  }
}

// Configurar file inputs con wrapper y validacion
const fileInputs = ['logoEntidad', 'resolucion', 'formatoTerceros', 'soportePago', 'certificadoExistencia'];
fileInputs.forEach(id => {
  const fileInput = document.getElementById(id);
  const wrapper = document.getElementById(id + 'Wrapper');
  const textSpan = document.getElementById(id + 'Text');
  
  if (wrapper) {
    wrapper.addEventListener('click', () => fileInput.click());
    fileInput.addEventListener('change', () => {
      clearFileError(id);
      if (fileInput.files && fileInput.files.length > 0) {
        const file = fileInput.files[0];
        const validation = validateFile(id, file);
        textSpan.textContent = file.name;
        if (!validation.valid) {
          showFileError(id, validation.message);
        }
      } else {
        textSpan.textContent = 'Ningún archivo seleccionado';
      }
    });
  }
});
