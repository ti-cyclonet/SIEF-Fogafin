const API_BASE_URL = "http://localhost:7176/api/";
const buscarEntidad = document.getElementById("buscarEntidad");
const selectEntidad = document.getElementById("selectEntidad");
const informacionEntidad = document.getElementById("informacionEntidad");
const detalleEntidad = document.getElementById("detalleEntidad");
let entidadesData = [];
let comprobantesPago = [];

async function loadEntidadesGestionables() {
  const userArea = localStorage.getItem('userArea');
  const isDOT = userArea === '52050';
  const estadosGestionables = isDOT ? "13" : "1,2,3,4,5,6,7,8,9,10,11,12";
  const url = `${API_BASE_URL}entidades-filtradas?estadoIds=${estadosGestionables}`;
  try {
    const response = await fetch(url);
    if (!response.ok) throw new Error("Error al cargar entidades para gestión.");
    const entidades = await response.json();
    entidadesData = entidades;
    selectEntidad.innerHTML = '<option value="">Seleccione...</option>';
    entidades.forEach((e) => {
      const option = document.createElement('option');
      option.value = e.Id;
      option.textContent = e.RazonSocial;
      option.dataset.estadoId = e.EstadoId;
      option.dataset.estadoNombre = e.EstadoNombre;
      selectEntidad.appendChild(option);
    });
  } catch (error) {
    Swal.fire("Error de Carga", "No se pudieron cargar las entidades gestionables.", "error");
    console.error(error);
  }
}

async function descargarComprobanteDesdeId(tn07Id) {
  try {
    const response = await fetch(`${API_BASE_URL}ObtenerArchivoDesdeId/${tn07Id}`);
    if (!response.ok) throw new Error('Error al obtener el archivo');
    const data = await response.json();
    if (data.url) {
      const downloadUrl = `${API_BASE_URL}DescargarArchivo?url=${encodeURIComponent(data.url)}&inline=true`;
      window.open(downloadUrl, '_blank');
    } else Swal.fire('Error', 'No se pudo obtener la URL del archivo', 'error');
  } catch (error) {
    Swal.fire('Error', 'No se pudo visualizar el archivo', 'error');
  }
}

function formatearMoneda(valor) {
  return "$ " + Number(valor).toLocaleString("es-CO", {minimumFractionDigits: 0, maximumFractionDigits: 0});
}

function formatearMonedaConDecimales(valor) {
  return "$ " + Number(valor).toLocaleString("es-CO", {minimumFractionDigits: 2, maximumFractionDigits: 2});
}

function mostrarEntidades(entidades) {
  const selectEntidad = document.getElementById('selectEntidad');
  selectEntidad.innerHTML = '<option value="">Seleccione...</option>';
  entidades.forEach((entidad) => {
    const option = document.createElement('option');
    option.value = entidad.Id;
    option.textContent = entidad.RazonSocial;
    selectEntidad.appendChild(option);
  });
}

function filtrarEntidades(texto) {
  if (!texto) {
    mostrarEntidades(entidadesData);
    return;
  }
  const entidadesFiltradas = entidadesData.filter(entidad => 
    entidad.RazonSocial.toLowerCase().includes(texto.toLowerCase())
  );
  mostrarEntidades(entidadesFiltradas);
}

async function cargarDetalleEntidad(entidadId, entidadData = {}) {
  if (!entidadId) {
    limpiarDetalles();
    return;
  }
  try {
    const response = await fetch(`${API_BASE_URL}ConsultarDetalleEntidad/${entidadId}`);
    if (!response.ok) throw new Error('Error al obtener el detalle de la entidad');
    const detalle = await response.json();
    const detalleCompleto = { ...detalle, ...entidadData };
    mostrarDetalleEntidad(detalleCompleto);
  } catch (error) {
    console.error('Error al cargar detalle:', error);
    mostrarDetalleEntidad(entidadData);
  }
}

function cargarComprobanteInicial(detalle) {
  const tabla = document.querySelector('#tablaPagos tbody');
  if (!tabla) return;
  tabla.innerHTML = '';
  if (!detalle.pagos || detalle.pagos.length === 0) return;
  detalle.pagos.forEach(pago => {
    const fila = document.createElement('tr');
    const fecha = new Date(pago.TN06_Fecha).toLocaleDateString('es-CO');
    const valor = formatearMonedaConDecimales(pago.TN06_Valor);
    const tn07Id = parseInt(pago.TN06_Comprobante);
    const td = document.createElement('td');
    if (tn07Id) {
      const link = document.createElement('a');
      link.href = '#';
      link.className = 'text-primary';
      link.textContent = 'Ver archivo';
      link.addEventListener('click', (e) => {
        e.preventDefault();
        descargarComprobanteDesdeId(tn07Id);
      });
      td.appendChild(link);
    } else {
      td.innerHTML = '<a href="#" class="text-primary">No disponible</a>';
    }
    fila.innerHTML = `<td>${fecha}</td><td>${valor}</td>`;
    fila.appendChild(td);
    tabla.appendChild(fila);
  });
}

function mostrarDetalleEntidad(detalle) {
  document.getElementById('tipoEntidad').textContent = detalle.tipoEntidad || '';
  document.getElementById('nitEntidad').textContent = detalle.nit || '';
  document.getElementById('correoNotificacion').textContent = detalle.correoNotificacion || '';
  document.getElementById('paginaWeb').textContent = detalle.paginaWeb || '';
  document.getElementById('numeroTramite').textContent = detalle.numeroTramite || '';
  document.getElementById('selectEstado').textContent = detalle.estadoNombre || '';
  document.getElementById('nombreRepresentante').textContent = detalle.nombreRepresentante || 'Información xxxxx';
  document.getElementById('numeroDocumento').textContent = detalle.identificacionRepresentante || 'Información xxxxx';
  document.getElementById('correoRepresentante').textContent = detalle.correoRepresentante || 'Información xxxxx';
  document.getElementById('telefonoRepresentante').textContent = detalle.telefonoRepresentante || 'Información xxxxx';
  document.getElementById('nombreResponsable').textContent = detalle.nombreResponsableRegistro || 'Información xxxxx';
  document.getElementById('correoResponsable').textContent = detalle.correoResponsableRegistro || 'Información xxxxx';
  document.getElementById('telefonoResponsable').textContent = detalle.telefonoResponsableRegistro || 'Información xxxxx';
  const fechaConst = detalle.fechaConstitucion ? new Date(detalle.fechaConstitucion).toLocaleDateString('es-CO') : 'Información xxxxx';
  document.getElementById('fechaConstitucion').textContent = fechaConst;
  document.getElementById('capitalSuscrito').textContent = detalle.capitalSuscrito ? formatearMoneda(detalle.capitalSuscrito) : 'Información xxxxx';
  const valorPagadoCalculado = detalle.capitalSuscrito ? detalle.capitalSuscrito * 0.000115 : 0;
  document.getElementById('valorPagado').textContent = valorPagadoCalculado > 0 ? formatearMonedaConDecimales(valorPagadoCalculado) : 'Información xxxxx';
  document.getElementById('fechaPago').textContent = detalle.fechaPago || 'Información xxxxx';
  cargarComprobanteInicial(detalle);
  window.currentDetalle = detalle;
  configurarLinksArchivos(detalle.archivos || [], detalle.rutaComprobantePago);
  actualizarBotonesGestion(detalle.estadoNombre);
  controlarEditabilidadInformacionGeneral();
  informacionEntidad.classList.remove('d-none');
  detalleEntidad.classList.remove('d-none');
}

function obtenerEntidadSeleccionadaId() {
  const selectEntidad = document.getElementById('selectEntidad');
  const selectedOption = selectEntidad.options[selectEntidad.selectedIndex];
  return selectedOption ? selectedOption.value : null;
}

function configurarLinksArchivos(archivos, rutaComprobantePago = null) {
  const links = {
    'linkCertificado': 'CERTIFICADO_',
    'linkResolucion': 'RESOLUCION_',
    'linkInscripcion': 'TERCEROS_',
    'linkLogo': 'LOGO_',
    'linkSoportePago': 'PAGO_',
    'linkComprobanteTabla': 'COMPROBANTE_'
  };
  Object.keys(links).forEach(linkId => {
    const linkElement = document.getElementById(linkId);
    if (!linkElement) return;
    const prefijo = links[linkId];
    let archivo = archivos && archivos.find(a => a.includes(prefijo));
    if ((linkId === 'linkSoportePago' || linkId === 'linkComprobanteTabla') && rutaComprobantePago) {
      archivo = rutaComprobantePago;
    }
    if (archivo) {
      const downloadUrl = `${API_BASE_URL}DescargarArchivo?url=${encodeURIComponent(archivo)}`;
      linkElement.href = downloadUrl;
      linkElement.target = '_blank';
      linkElement.textContent = 'Ver archivo';
      linkElement.style.pointerEvents = 'auto';
      linkElement.style.color = '';
    } else {
      linkElement.href = '#';
      linkElement.target = '';
      linkElement.textContent = 'No disponible';
      linkElement.style.pointerEvents = 'none';
      linkElement.style.color = '#6c757d';
    }
  });
}

async function aprobarDocumentos() {
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
    
    const response = await fetch(`${API_BASE_URL}AprobarDocumentos`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        EntidadId: parseInt(entidadId),
        RazonSocial: detalle.razonSocial,
        NumeroTramite: detalle.numeroTramite,
        Funcionario: funcionario
      })
    });

    if (!response.ok) throw new Error('Error al aprobar documentos');
    
    Swal.fire({
      icon: 'success',
      title: 'Documentos aprobados',
      text: 'El estado ha cambiado a "En validación del pago" y se ha enviado notificación a DOT',
      timer: 2000,
      showConfirmButton: false
    });

    detalle.estadoNombre = 'En validación del pago';
    document.getElementById('selectEstado').textContent = 'En validación del pago';
    actualizarBotonesGestion('En validación del pago');
  } catch (error) {
    Swal.fire('Error', 'No se pudo aprobar los documentos', 'error');
  }
}

function limpiarTodo() {
  document.getElementById('buscarEntidad').value = '';
  document.getElementById('selectEntidad').classList.add('d-none');
  limpiarDetalles();
}

function toggleModificarCapital() {
  const capitalSpan = document.getElementById('capitalSuscrito');
  const btnModificar = document.getElementById('btnModificarCapital');
  if (btnModificar.textContent.includes('Modificar')) {
    const valorActual = capitalSpan.textContent.replace(/[^\d]/g, '');
    capitalSpan.innerHTML = `<input type="number" id="inputCapitalSuscrito" class="form-control d-inline" style="width: 200px;" value="${valorActual}" min="0">`;
    btnModificar.innerHTML = '<i class="fas fa-save me-1"></i>Guardar';
    btnModificar.className = 'btn btn-sm btn-success ms-2';
    const btnCancelar = document.createElement('button');
    btnCancelar.innerHTML = '<i class="fas fa-times me-1"></i>Cancelar';
    btnCancelar.className = 'btn btn-sm btn-secondary ms-2';
    btnCancelar.id = 'btnCancelarCapital';
    btnModificar.parentNode.appendChild(btnCancelar);
    btnCancelar.addEventListener('click', () => {
      capitalSpan.textContent = formatearMoneda(valorActual);
      btnModificar.innerHTML = 'Modificar capital suscrito';
      btnModificar.className = 'btn btn-sm btn-outline-secondary ms-2';
      btnCancelar.remove();
    });
    document.getElementById('inputCapitalSuscrito').focus();
  } else {
    const inputCapital = document.getElementById('inputCapitalSuscrito');
    const nuevoValor = inputCapital.value;
    if (nuevoValor && !isNaN(nuevoValor) && nuevoValor > 0) {
      guardarCapitalSuscrito(nuevoValor, capitalSpan, btnModificar);
    } else {
      Swal.fire('Error', 'Ingrese un valor válido mayor a 0', 'error');
    }
  }
}

async function guardarCapitalSuscrito(nuevoValor, capitalSpan, btnModificar) {
  const entidadId = obtenerEntidadSeleccionadaId();
  try {
    Swal.fire({title: 'Guardando...', allowOutsideClick: false, didOpen: () => Swal.showLoading()});
    const response = await fetch(`${API_BASE_URL}ActualizarCapitalSuscrito`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({EntidadId: parseInt(entidadId), CapitalSuscrito: parseFloat(nuevoValor)})
    });
    if (!response.ok) throw new Error('Error al actualizar el capital');
    const valorPagadoCalculado = nuevoValor * 0.000115;
    capitalSpan.textContent = formatearMoneda(nuevoValor);
    document.getElementById('valorPagado').textContent = formatearMonedaConDecimales(valorPagadoCalculado);
    btnModificar.innerHTML = 'Modificar capital suscrito';
    btnModificar.className = 'btn btn-sm btn-outline-secondary ms-2';
    const btnCancelar = document.getElementById('btnCancelarCapital');
    if (btnCancelar) btnCancelar.remove();
    Swal.fire({icon: 'success', title: 'Capital actualizado', text: `Capital: ${formatearMoneda(nuevoValor)} - Valor pagado: ${formatearMonedaConDecimales(valorPagadoCalculado)}`, timer: 2000, showConfirmButton: false});
  } catch (error) {
    Swal.fire('Error', 'No se pudo actualizar el capital suscrito', 'error');
  }
}

function mostrarModalComprobante() {
  Swal.fire({
    title: 'Adjuntar comprobante de pago',
    html: `<div class="text-start"><div class="mb-3"><label class="form-label">Fecha de pago:</label><input type="date" id="fechaComprobante" class="form-control" max="${new Date().toISOString().split('T')[0]}"></div><div class="mb-3"><label class="form-label">Valor:</label><input type="number" id="valorComprobante" class="form-control" min="0" step="0.01"></div><div class="mb-3"><label class="form-label">Comprobante de pago:</label><div class="file-wrapper" id="fileWrapper"><span class="text-muted flex-grow-1" id="archivoTexto">Ningún archivo seleccionado</span><span class="badge bg-light text-dark">Seleccionar archivos</span><input type="file" id="archivoComprobante" accept=".pdf"></div></div></div>`,
    showCancelButton: true,
    confirmButtonText: 'Agregar',
    cancelButtonText: 'Cancelar',
    preConfirm: () => {
      const fecha = document.getElementById('fechaComprobante').value;
      const valor = document.getElementById('valorComprobante').value;
      const archivo = document.getElementById('archivoComprobante').files[0];
      if (!fecha || !valor || !archivo) {
        Swal.showValidationMessage('Todos los campos son obligatorios');
        return false;
      }
      if (parseFloat(valor) <= 0) {
        Swal.showValidationMessage('El valor debe ser mayor a 0');
        return false;
      }
      return { fecha, valor: parseFloat(valor), archivo };
    }
  }).then(async (result) => {
    if (result.isConfirmed) {
      await subirArchivoYAgregarComprobante(result.value);
    }
  });
  setTimeout(() => {
    const wrapper = document.getElementById('fileWrapper');
    const input = document.getElementById('archivoComprobante');
    const texto = document.getElementById('archivoTexto');
    if (wrapper && input && texto) {
      wrapper.onclick = function() { input.click(); };
      input.onchange = function() {
        if (this.files && this.files.length > 0) {
          texto.textContent = this.files[0].name;
          texto.className = 'text-dark flex-grow-1';
        } else {
          texto.textContent = 'Ningún archivo seleccionado';
          texto.className = 'text-muted flex-grow-1';
        }
      };
    }
  }, 200);
}

async function subirArchivoYAgregarComprobante(comprobante) {
  try {
    Swal.fire({title: 'Subiendo archivo...', allowOutsideClick: false, didOpen: () => Swal.showLoading()});
    const fileBase64 = await convertirArchivoABase64(comprobante.archivo);
    const response = await fetch(`${API_BASE_URL}SubirComprobantePago?entidadId=${obtenerEntidadSeleccionadaId()}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({archivoBase64: fileBase64, nombreArchivo: comprobante.archivo.name, valor: comprobante.valor, fechaPago: comprobante.fecha})
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
    Swal.fire({icon: 'success', title: 'Comprobante agregado', text: 'El archivo se ha subido correctamente', timer: 2000, showConfirmButton: false});
  } catch (error) {
    Swal.fire('Error', 'No se pudo subir el archivo', 'error');
  }
}

function convertirArchivoABase64(archivo) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const base64 = reader.result.split(',')[1];
      resolve(base64);
    };
    reader.onerror = reject;
    reader.readAsDataURL(archivo);
  });
}

function limpiarDetalles() {
  document.getElementById('tipoEntidad').textContent = '';
  document.getElementById('nitEntidad').textContent = '';
  document.getElementById('correoNotificacion').textContent = '';
  document.getElementById('paginaWeb').textContent = '';
  document.getElementById('numeroTramite').textContent = '';
  document.getElementById('selectEstado').textContent = '';
  document.getElementById('capitalSuscrito').textContent = 'Información xxxxx';
  document.getElementById('valorPagado').textContent = 'Información xxxxx';
  document.getElementById('fechaPago').textContent = 'Información xxxxx';
  document.getElementById('nombreRepresentante').textContent = 'Información xxxxx';
  document.getElementById('numeroDocumento').textContent = 'Información xxxxx';
  document.getElementById('correoRepresentante').textContent = 'Información xxxxx';
  document.getElementById('telefonoRepresentante').textContent = 'Información xxxxx';
  document.getElementById('nombreResponsable').textContent = 'Información xxxxx';
  document.getElementById('correoResponsable').textContent = 'Información xxxxx';
  document.getElementById('telefonoResponsable').textContent = 'Información xxxxx';
  const tablaPagos = document.querySelector('#tablaPagos tbody');
  if (tablaPagos) tablaPagos.innerHTML = '';
  comprobantesPago = [];
  document.getElementById('btnAprobarDocumentos').disabled = true;

  document.getElementById('btnRechazarInscripcion').addEventListener('click', () => {
    Swal.fire('Información', 'Funcionalidad de rechazar inscripción en desarrollo', 'info');
  });
  informacionEntidad.classList.add('d-none');
  detalleEntidad.classList.add('d-none');
}

function controlarVisibilidadPago() {
  const userName = localStorage.getItem('currentUser');
  const userArea = localStorage.getItem('userArea');
  const userRole = localStorage.getItem('userRole');
  const isAdmin = userName && userName.toLowerCase() === 'adminsief';
  const isJefeSSD = userArea === '59030' && userRole === 'Jefe';
  const isDOT = userArea === '52050';
  const elementosPago = [
    document.getElementById('btnConfirmarPago'),
    document.getElementById('btnCancelarPago'),
    document.getElementById('btnAdjuntarComprobante')
  ];
  const seccionValidacion = document.querySelector('#informacionPago h6.text-success:nth-of-type(3)');
  const tablaYBotones = document.querySelector('#informacionPago .row');
  
  const mostrar = isDOT || isAdmin || isJefeSSD;
  elementosPago.forEach(el => {
    if (el) el.style.display = mostrar ? '' : 'none';
  });
  if (seccionValidacion) seccionValidacion.style.display = mostrar ? '' : 'none';
  if (tablaYBotones) tablaYBotones.style.display = mostrar ? '' : 'none';
}

function controlarEditabilidadInformacionGeneral() {
  const userArea = localStorage.getItem('userArea');
  const isDOT = userArea === '52050';
  if (isDOT) {
    const btnAprobarDocumentos = document.getElementById('btnAprobarDocumentos');
    const btnRechazarInscripcion = document.getElementById('btnRechazarInscripcion');
    const btnModificarCapital = document.getElementById('btnModificarCapital');
    const documentosAdicionalesWrapper = document.getElementById('documentosAdicionalesWrapper');
    
    if (btnAprobarDocumentos) btnAprobarDocumentos.style.display = 'none';
    if (btnRechazarInscripcion) btnRechazarInscripcion.style.display = 'none';
    if (btnModificarCapital) btnModificarCapital.style.display = 'none';
    if (documentosAdicionalesWrapper) documentosAdicionalesWrapper.style.pointerEvents = 'none';
  }
}

document.addEventListener("DOMContentLoaded", () => {
  const userName = localStorage.getItem('currentUser');
  const funcionarioSpan = document.getElementById('nombreFuncionario');
  const departamentoSpan = document.getElementById('departamentoFuncionario');
  if (userName && funcionarioSpan) {
    const parts = userName.split('.');
    const displayUser = parts.map(p => p.charAt(0).toUpperCase() + p.slice(1)).join(' ');
    funcionarioSpan.textContent = displayUser;
    const userAreaName = localStorage.getItem('userAreaName');
    let tipoUsuario = 'Funcionario';
    let departamento = '';
    if (userName.toLowerCase() === 'adminsief') {
      tipoUsuario = 'Administrador';
    } else if (userAreaName) {
      departamento = userAreaName;
    } else {
      departamento = 'SSD';
    }
    departamentoSpan.textContent = departamento ? `${tipoUsuario} ${departamento}` : tipoUsuario;
  } else {
    window.location.href = '../index.html';
    return;
  }
  controlarVisibilidadPago();
  document.getElementById('btnSalir').addEventListener('click', () => {
    localStorage.removeItem('currentUser');
    localStorage.removeItem('userArea');
    localStorage.removeItem('userAreaName');
    window.location.href = '../index.html';
  });
  loadEntidadesGestionables();
  const documentosAdicionales = document.getElementById('documentosAdicionales');
  const documentosAdicionalesWrapper = document.getElementById('documentosAdicionalesWrapper');
  const documentosAdicionalesText = document.getElementById('documentosAdicionalesText');
  documentosAdicionalesWrapper.addEventListener('click', () => { documentosAdicionales.click(); });
  documentosAdicionales.addEventListener('change', function() {
    if (this.files.length > 0) {
      documentosAdicionalesText.textContent = this.files.length === 1 ? this.files[0].name : `${this.files.length} archivos seleccionados`;
      documentosAdicionalesText.className = 'text-dark flex-grow-1';
    } else {
      documentosAdicionalesText.textContent = 'Ningún archivo seleccionado';
      documentosAdicionalesText.className = 'text-muted flex-grow-1';
    }
  });
  const documentosAdicionalesPago = document.getElementById('documentosAdicionalesPago');
  const documentosAdicionalesPagoWrapper = document.getElementById('documentosAdicionalesPagoWrapper');
  const documentosAdicionalesPagoText = document.getElementById('documentosAdicionalesPagoText');
  documentosAdicionalesPagoWrapper.addEventListener('click', () => { documentosAdicionalesPago.click(); });
  documentosAdicionalesPago.addEventListener('change', function() {
    if (this.files.length > 0) {
      documentosAdicionalesPagoText.textContent = this.files.length === 1 ? this.files[0].name : `${this.files.length} archivos seleccionados`;
      documentosAdicionalesPagoText.className = 'text-dark flex-grow-1';
    } else {
      documentosAdicionalesPagoText.textContent = 'Ningún archivo seleccionado';
      documentosAdicionalesPagoText.className = 'text-muted flex-grow-1';
    }
  });
  buscarEntidad.addEventListener('input', (e) => {
    const texto = e.target.value;
    filtrarEntidades(texto);
    if (texto && entidadesData.length > 0) {
      selectEntidad.classList.remove('d-none');
    } else {
      selectEntidad.classList.add('d-none');
    }
  });
  buscarEntidad.addEventListener('focus', () => {
    if (entidadesData.length > 0) {
      selectEntidad.classList.remove('d-none');
    }
  });
  document.addEventListener('click', (e) => {
    if (!e.target.closest('.position-relative')) {
      selectEntidad.classList.add('d-none');
    }
  });
  selectEntidad.addEventListener('change', (e) => {
    const selectedOption = e.target.options[e.target.selectedIndex];
    if (selectedOption.value) {
      buscarEntidad.value = selectedOption.textContent;
      selectEntidad.classList.add('d-none');
      const entidadData = { estadoNombre: selectedOption.dataset.estadoNombre || '' };
      cargarDetalleEntidad(selectedOption.value, entidadData);
    }
  });
  document.getElementById('btnLimpiar').addEventListener('click', limpiarTodo);
  document.getElementById('btnCerrar').addEventListener('click', () => {
    limpiarTodo();
    window.location.href = 'dashboard.html';
  });
  document.getElementById('btnModificarCapital').addEventListener('click', toggleModificarCapital);
  document.getElementById('btnAdjuntarComprobante').addEventListener('click', mostrarModalComprobante);
  document.getElementById('btnAprobarDocumentos').addEventListener('click', aprobarDocumentos);
  document.getElementById('btnConfirmarPago').addEventListener('click', confirmarPago);
  document.getElementById('btnCancelarPago').addEventListener('click', () => {
    Swal.fire('Información', 'Funcionalidad de cancelar pago en desarrollo', 'info');
  });
  document.getElementById('btnAprobarInscripcion').addEventListener('click', aprobarInscripcion);
});
