// Reemplazar la función subirArchivoYAgregarComprobante
async function subirArchivoYAgregarComprobante(comprobante) {
  try {
    Swal.fire({
      title: 'Subiendo archivo...',
      allowOutsideClick: false,
      didOpen: () => Swal.showLoading()
    });

    const fileBase64 = await convertirArchivoABase64(comprobante.archivo);
    
    const response = await fetch(`${API_BASE_URL}SubirComprobantePago?entidadId=${obtenerEntidadSeleccionadaId()}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        archivoBase64: fileBase64,
        nombreArchivo: comprobante.archivo.name,
        valor: comprobante.valor,
        fechaPago: comprobante.fecha
      })
    });

    if (!response.ok) throw new Error('Error al subir archivo');

    const resultado = await response.json();
    const tabla = document.querySelector('#tablaPagos tbody');
    if (tabla) {
      const fila = document.createElement('tr');
      const fecha = new Date(comprobante.fecha).toLocaleDateString('es-CO');
      const valor = formatearMonedaConDecimales(comprobante.valor);
      fila.innerHTML = `<td>${fecha}</td><td>${valor}</td><td><a href="${resultado.url}" target="_blank" class="text-primary">Ver archivo</a></td>`;
      tabla.appendChild(fila);
    }
    Swal.fire({
      icon: 'success',
      title: 'Comprobante agregado',
      text: 'El archivo se ha subido correctamente',
      timer: 2000,
      showConfirmButton: false
    });
  } catch (error) {
    Swal.fire('Error', 'No se pudo subir el archivo', 'error');
  }
}
