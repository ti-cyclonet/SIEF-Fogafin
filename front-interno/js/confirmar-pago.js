async function confirmarPago() {
  const entidadId = obtenerEntidadSeleccionadaId();
  const detalle = window.currentDetalle;
  
  if (!entidadId || !detalle) {
    Swal.fire('Error', 'No hay entidad seleccionada', 'error');
    return;
  }

  const tabla = document.querySelector('#tablaPagos tbody');
  if (!tabla || tabla.rows.length === 0) {
    Swal.fire('Warning', 'Debe adjuntar los extractos con la información del pago', 'warning');
    return;
  }

  let sumaComprobantes = 0;
  for (let row of tabla.rows) {
    const valorTexto = row.cells[1].textContent.replace(/[^\d,.-]/g, '').replace(/\./g, '').replace(',', '.');
    sumaComprobantes += parseFloat(valorTexto) || 0;
  }

  const valorPagadoTexto = document.getElementById('valorPagado').textContent.replace(/[^\d,.-]/g, '').replace(/\./g, '').replace(',', '.');
  const valorPagado = parseFloat(valorPagadoTexto) || 0;

  if (Math.abs(sumaComprobantes - valorPagado) > 0.01) {
    Swal.fire('Error', `Los valores no coinciden. Suma de comprobantes: ${formatearMonedaConDecimales(sumaComprobantes)} vs Valor pagado: ${formatearMonedaConDecimales(valorPagado)}`, 'error');
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
      timer: 2000,
      showConfirmButton: false
    }).then(() => {
      location.reload();
    });

    detalle.estadoNombre = 'Pendiente de aprobación final';
    document.getElementById('selectEstado').textContent = 'Pendiente de aprobación final';
    
    // Deshabilitar botones de pago
    document.getElementById('btnConfirmarPago').disabled = true;
    document.getElementById('btnCancelarPago').disabled = true;
  } catch (error) {
    Swal.fire('Error', 'No se pudo confirmar el pago', 'error');
  }
}
