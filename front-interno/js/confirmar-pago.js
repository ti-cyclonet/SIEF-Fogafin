async function confirmarPago() {
  const entidadId = obtenerEntidadSeleccionadaId();
  const detalle = window.currentDetalle;
  
  if (!entidadId || !detalle) {
    Swal.fire('Error', 'No hay entidad seleccionada', 'error');
    return;
  }

  const tabla = document.querySelector('#tablaPagos tbody');
  if (!tabla || tabla.rows.length === 0) {
    Swal.fire('Warning', 'Debe adjuntar al menos un extracto con la información del pago', 'warning');
    return;
  }

  // Calcular suma de todos los extractos
  let sumaExtractos = 0;
  for (let row of tabla.rows) {
    const valor = parseFloat(row.dataset.valor) || 0;
    sumaExtractos += valor;
  }

  // Obtener valor esperado del pago (desde el campo valorPagado)
  const valorPagadoElement = document.getElementById('valorPagado');
  const valorEsperadoTexto = valorPagadoElement ? valorPagadoElement.textContent.replace(/[^\d,.-]/g, '').replace(/\./g, '').replace(',', '.') : '0';
  const valorEsperado = parseFloat(valorEsperadoTexto) || 0;

  // Validar que la suma de extractos coincida con el valor esperado
  const diferencia = Math.abs(sumaExtractos - valorEsperado);
  if (diferencia > 0.01) {
    Swal.fire({
      icon: 'error',
      title: 'Los valores no coinciden',
      text: 'La sumatoria de los valores incluidos en los comprobantes debe ser igual al monto indicado en valor pagado por inscripción.'
    });
    return;
  }

  try {
    Swal.fire({title: 'Procesando...', allowOutsideClick: false, didOpen: () => Swal.showLoading()});
    
    const currentUser = localStorage.getItem('currentUser');
    const funcionario = currentUser ? currentUser.replace(/\./g, '') : '';
    
    const response = await fetch(getApiUrl('ConfirmarPago'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        EntidadId: parseInt(entidadId),
        NumeroTramite: detalle.numeroTramite,
        usuario: funcionario
      })
    });

    if (!response.ok) throw new Error('Error al confirmar pago');
    
    Swal.fire({
      icon: 'success',
      title: 'Pago confirmado',
      text: 'El estado ha cambiado a "Pendiente de aprobación final"',
      confirmButtonText: 'Cerrar'
    }).then(async () => {
      await loadEntidadesGestionables();
      const buscarEntidad = document.getElementById('buscarEntidad');
      const selectEntidad = document.getElementById('selectEntidad');
      if (buscarEntidad) buscarEntidad.value = '';
      if (selectEntidad) selectEntidad.classList.add('d-none');
      limpiarDetalles();
    });
  } catch (error) {
    Swal.fire('Error', 'No se pudo confirmar el pago', 'error');
    console.error('Error al confirmar pago:', error);
  }
}
