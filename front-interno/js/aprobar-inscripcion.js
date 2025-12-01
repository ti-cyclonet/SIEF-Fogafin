function actualizarBotonesGestion(estadoNombre) {
  const btnAprobarDocs = document.getElementById('btnAprobarDocumentos');
  const btnAprobarInsc = document.getElementById('btnAprobarInscripcion');
  const btnRechazarInsc = document.getElementById('btnRechazarInscripcion');
  const btnModificarCapital = document.getElementById('btnModificarCapital');
  const btnAdjuntarComprobante = document.getElementById('btnAdjuntarComprobante');
  const btnConfirmarPago = document.getElementById('btnConfirmarPago');
  const btnCancelarPago = document.getElementById('btnCancelarPago');
  
  btnAprobarDocs.disabled = true;
  btnAprobarInsc.disabled = true;
  btnRechazarInsc.disabled = false;
  btnModificarCapital.disabled = false;
  btnAdjuntarComprobante.disabled = false;
  btnConfirmarPago.disabled = false;
  btnCancelarPago.disabled = false;
  
  if (estadoNombre && estadoNombre.toLowerCase().includes('validación de documentos')) {
    btnAprobarDocs.disabled = false;
  }
  if (estadoNombre && estadoNombre.toLowerCase().includes('pendiente de aprobación final')) {
    btnAprobarInsc.disabled = false;
    btnAprobarDocs.disabled = true;
    btnRechazarInsc.disabled = true;
    btnModificarCapital.disabled = true;
    btnAdjuntarComprobante.disabled = true;
    btnConfirmarPago.disabled = true;
    btnCancelarPago.disabled = true;
  }
}

async function aprobarInscripcion() {
  const entidadId = obtenerEntidadSeleccionadaId();
  const detalle = window.currentDetalle;
  
  if (!entidadId || !detalle) {
    Swal.fire('Error', 'No hay entidad seleccionada', 'error');
    return;
  }

  try {
    Swal.fire({title: 'Procesando...', allowOutsideClick: false, didOpen: () => Swal.showLoading()});
    
    const currentUser = localStorage.getItem('currentUser');
    const funcionario = currentUser ? currentUser.replace(/\./g, '') : '';
    
    const response = await fetch(`${API_BASE_URL}AprobarInscripcion`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        EntidadId: parseInt(entidadId),
        NumeroTramite: detalle.numeroTramite,
        Funcionario: funcionario
      })
    });

    if (!response.ok) throw new Error('Error al aprobar inscripción');
    
    Swal.fire({
      icon: 'success',
      title: 'Inscripción aprobada',
      text: 'El estado ha cambiado a "Entidad inscrita" y el proceso ha finalizado',
      timer: 2000,
      showConfirmButton: false
    });

    detalle.estadoNombre = 'Entidad inscrita';
    document.getElementById('selectEstado').textContent = 'Entidad inscrita';
    actualizarBotonesGestion('Entidad inscrita');
  } catch (error) {
    Swal.fire('Error', 'No se pudo aprobar la inscripción', 'error');
  }
}
