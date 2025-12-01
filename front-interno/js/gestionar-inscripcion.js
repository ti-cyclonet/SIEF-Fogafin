// Usar configuración del config.js (se carga desde el HTML)
const API_BASE_URL = CLOUD_CONFIG?.API_BASE_URL || `http://localhost:${7000 + 176}/api`;
let buscarEntidad, selectEntidad, informacionEntidad, detalleEntidad;
let entidadesData = [];
let comprobantesPago = [];

document.addEventListener("DOMContentLoaded", () => {
  buscarEntidad = document.getElementById("buscarEntidad");
  selectEntidad = document.getElementById("selectEntidad");
  informacionEntidad = document.getElementById("informacionEntidad");
  detalleEntidad = document.getElementById("detalleEntidad");
  
  if (!buscarEntidad || !selectEntidad || !informacionEntidad || !detalleEntidad) {
    console.error("Elementos principales no encontrados");
    return;
  }
  
  initializeApp();
});

async function loadEntidadesGestionables() {
  const userPerfil = localStorage.getItem('userPerfil');
  const isDOTProfile = userPerfil === 'Profesional DOT';
  
  let estadosGestionables;
  if (isDOTProfile) {
    estadosGestionables = "13"; // Solo estado 13 para perfil DOT
  } else {
    estadosGestionables = "12,13,14"; // Todos los estados para otros perfiles
  }

  
  const url = getApiUrl(`entidades-filtradas?estadoIds=${estadosGestionables}`);
  try {
    const response = await fetch(url);
    if (!response.ok) throw new Error("Error al cargar entidades para gestión.");
    const entidades = await response.json();
    
    // Filtro adicional en frontend para asegurar estados correctos
    const estadosPermitidos = estadosGestionables.split(',').map(id => parseInt(id));
    const entidadesFiltradas = entidades.filter(e => {
      const estadoId = e.EstadoId || e.estadoId;
      return estadosPermitidos.includes(estadoId);
    });
    
    entidadesData = entidadesFiltradas;
    if (selectEntidad) {
      selectEntidad.innerHTML = '<option value="">Seleccione...</option>';
      entidadesFiltradas.forEach((e) => {
        const option = document.createElement('option');
        option.value = e.Id || e.id;
        option.textContent = e.RazonSocial || e.razonSocial;
        option.dataset.estadoId = e.EstadoId || e.estadoId;
        option.dataset.estadoNombre = e.EstadoNombre || e.estadoNombre;
        selectEntidad.appendChild(option);
      });
    }
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

async function descargarArchivoDirecto(archivoUrl) {
  try {
    const downloadUrl = `${API_BASE_URL}DescargarArchivo?url=${encodeURIComponent(archivoUrl)}&inline=true`;
    window.open(downloadUrl, '_blank');
  } catch (error) {
    Swal.fire('Error', 'No se pudo visualizar el archivo', 'error');
  }
}

function formatearMoneda(valor) {
  return "$" + Number(valor).toLocaleString("es-CO", {minimumFractionDigits: 0, maximumFractionDigits: 0});
}

function formatearMonedaConDecimales(valor) {
  return "$" + Number(valor).toLocaleString("es-CO", {minimumFractionDigits: 2, maximumFractionDigits: 2});
}

function mostrarEntidades(entidades) {
  if (!selectEntidad) return;
  selectEntidad.innerHTML = '<option value="">Seleccione...</option>';
  entidades.forEach((entidad) => {
    const option = document.createElement('option');
    option.value = entidad.Id || entidad.id;
    option.textContent = entidad.RazonSocial || entidad.razonSocial;
    option.dataset.estadoId = entidad.EstadoId || entidad.estadoId;
    option.dataset.estadoNombre = entidad.EstadoNombre || entidad.estadoNombre;
    selectEntidad.appendChild(option);
  });
}

function filtrarEntidades(texto) {
  if (!texto) {
    mostrarEntidades(entidadesData);
    return;
  }
  const entidadesFiltradas = entidadesData.filter(entidad => 
    (entidad.RazonSocial || entidad.razonSocial || '').toLowerCase().includes(texto.toLowerCase())
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
    const detalleCompleto = { ...detalle, ...entidadData, id: entidadId };
    mostrarDetalleEntidad(detalleCompleto);
  } catch (error) {
    console.error('Error al cargar detalle:', error);
    mostrarDetalleEntidad({ ...entidadData, id: entidadId });
  }
}

function cargarComprobanteInicial(detalle) {
  const tabla = document.querySelector('#tablaPagos tbody');
  if (!tabla) return;
  tabla.innerHTML = '';
}

async function cargarHistorialGestion(entidadId) {
  const tabla = document.querySelector('#tablaHistorial tbody');
  if (!tabla) return;
  
  tabla.innerHTML = '<tr><td colspan="5" class="text-center"><i class="fas fa-spinner fa-spin"></i> Cargando historial...</td></tr>';
  
  try {
    const response = await fetch(`${API_BASE_URL}ConsultarHistorialGestion/${entidadId}`);
    if (!response.ok) throw new Error('Error al cargar historial');
    
    const historial = await response.json();
    tabla.innerHTML = '';
    
    if (historial.length === 0) {
      tabla.innerHTML = '<tr><td colspan="5" class="text-center text-muted">No hay registros de historial</td></tr>';
      return;
    }
    
    historial.forEach(registro => {
      const fila = document.createElement('tr');
      const fecha = new Date(registro.TN05_Fecha).toLocaleDateString('es-CO', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit'
      });
      
      fila.innerHTML = `
        <td>${fecha}</td>
        <td>${registro.EstadoAnterior || '-'}</td>
        <td><span class="badge bg-primary">${registro.EstadoActual}</span></td>
        <td>${registro.TN05_TN03_Usuario || '-'}</td>
        <td>${registro.TN05_Observaciones || '-'}</td>
      `;
      tabla.appendChild(fila);
    });
  } catch (error) {
    tabla.innerHTML = '<tr><td colspan="5" class="text-center text-danger">Error al cargar el historial</td></tr>';
  }
}

function mostrarDetalleEntidad(detalle) {
  const elementos = [
    'tipoEntidad', 'nitEntidad', 'correoNotificacion', 'paginaWeb', 'numeroTramite', 'selectEstado',
    'nombreRepresentante', 'numeroDocumento', 'correoRepresentante', 'telefonoRepresentante',
    'nombreResponsable', 'correoResponsable', 'telefonoResponsable', 'fechaConstitucion',
    'capitalSuscrito', 'valorPagado', 'fechaPago'
  ];
  
  elementos.forEach(id => {
    const elemento = document.getElementById(id);
    if (elemento) {
      switch(id) {
        case 'tipoEntidad': elemento.textContent = detalle.tipoEntidad || ''; break;
        case 'nitEntidad': elemento.textContent = detalle.nit || ''; break;
        case 'correoNotificacion': elemento.textContent = detalle.correoNotificacion || ''; break;
        case 'paginaWeb': elemento.textContent = detalle.paginaWeb || ''; break;
        case 'numeroTramite': elemento.textContent = detalle.numeroTramite || ''; break;
        case 'selectEstado': elemento.textContent = detalle.estadoNombre || ''; break;
        case 'fechaConstitucion': 
          elemento.textContent = detalle.fechaConstitucion ? new Date(detalle.fechaConstitucion).toLocaleDateString('es-CO') : 'Información xxxxx';
          break;
        case 'capitalSuscrito': 
          elemento.textContent = detalle.capitalSuscrito ? formatearMoneda(detalle.capitalSuscrito) : 'Información xxxxx';
          break;
        case 'valorPagado':
          const valorPagadoCalculado = detalle.capitalSuscrito ? detalle.capitalSuscrito * 0.000115 : 0;
          elemento.textContent = valorPagadoCalculado > 0 ? formatearMonedaConDecimales(valorPagadoCalculado) : 'Información xxxxx';
          break;
        case 'fechaPago':
          elemento.textContent = detalle.fechaPago ? new Date(detalle.fechaPago).toLocaleDateString('es-CO') : 'Información xxxxx';
          break;
        case 'numeroDocumento': 
          elemento.textContent = detalle.identificacionRepresentante || 'Información xxxxx';
          break;
        case 'correoRepresentante': 
          elemento.textContent = detalle.correoRepresentante || 'Información xxxxx';
          break;
        case 'telefonoRepresentante': 
          elemento.textContent = detalle.telefonoRepresentante || 'Información xxxxx';
          break;
        case 'nombreResponsable': 
          elemento.textContent = detalle.nombreResponsableRegistro || 'Información xxxxx';
          break;
        case 'correoResponsable': 
          elemento.textContent = detalle.correoResponsableRegistro || 'Información xxxxx';
          break;
        case 'telefonoResponsable': 
          elemento.textContent = detalle.telefonoResponsableRegistro || 'Información xxxxx';
          break;
        case 'nombreRepresentante':
          elemento.textContent = detalle.nombreRepresentante || 'Información xxxxx';
          break;
        default:
          elemento.textContent = detalle[id] || 'Información xxxxx';
      }
    }
  });
  
  cargarComprobanteInicial(detalle);
  if (detalle.id) {
    cargarHistorialGestion(detalle.id);
  }
  window.currentDetalle = detalle;
  configurarLinksArchivos(detalle.archivos || [], detalle.rutaComprobantePago);
  actualizarBotonesGestion(detalle.estadoNombre, detalle.estadoId);
  controlarEditabilidadInformacionGeneral();
  if (informacionEntidad) informacionEntidad.classList.remove('d-none');
  if (detalleEntidad) detalleEntidad.classList.remove('d-none');
}

function obtenerEntidadSeleccionadaId() {
  if (!selectEntidad) return null;
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
      const downloadUrl = `${API_BASE_URL}DescargarArchivo?url=${encodeURIComponent(archivo)}&inline=true`;
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
      linkElement.style.color = '#000';
      linkElement.style.textDecoration = 'none';
    }
  });
  
  // Mostrar documentos adicionales
  const tablaDocumentos = document.querySelector('#tablaDocumentosAdicionales tbody');
  const documentosAdicionales = archivos ? archivos.filter(a => a.includes('ADICIONAL_SOLICITUD_')) : [];
  
  if (tablaDocumentos) {
    tablaDocumentos.innerHTML = '';
    documentosAdicionales.forEach((archivo, index) => {
      const newRow = document.createElement('tr');
      const link = document.createElement('a');
      link.href = '#';
      link.className = 'text-primary';
      link.textContent = 'Ver archivo';
      link.addEventListener('click', (e) => {
        e.preventDefault();
        descargarArchivoDirecto(archivo);
      });
      
      const td1 = document.createElement('td');
      td1.className = 'fw-bold';
      td1.style.width = '40%';
      td1.textContent = `Documento [${index + 1}]:`;
      
      const td2 = document.createElement('td');
      td2.appendChild(link);
      
      newRow.appendChild(td1);
      newRow.appendChild(td2);
      tablaDocumentos.appendChild(newRow);
    });
  }
  
  // Mostrar documentos adicionales de pago
  const tablaDocumentosPago = document.querySelector('#tablaDocumentosAdicionalesPago tbody');
  const documentosAdicionalesPago = archivos ? archivos.filter(a => a.includes('ADICIONAL_PAGO_')) : [];
  
  if (tablaDocumentosPago) {
    tablaDocumentosPago.innerHTML = '';
    documentosAdicionalesPago.forEach((archivo, index) => {
      const newRow = document.createElement('tr');
      const link = document.createElement('a');
      link.href = '#';
      link.className = 'text-primary';
      link.textContent = 'Ver archivo';
      link.addEventListener('click', (e) => {
        e.preventDefault();
        descargarArchivoDirecto(archivo);
      });
      
      const td1 = document.createElement('td');
      td1.className = 'fw-bold';
      td1.style.width = '40%';
      td1.textContent = `Documento [${index + 1}]:`;
      
      const td2 = document.createElement('td');
      td2.appendChild(link);
      
      newRow.appendChild(td1);
      newRow.appendChild(td2);
      tablaDocumentosPago.appendChild(newRow);
    });
  }
}

function actualizarBotonesGestion(estadoNombre, estadoId) {
  const btnAprobarDocumentos = document.getElementById('btnAprobarDocumentos');
  const btnAprobarInscripcion = document.getElementById('btnAprobarInscripcion');
  const btnRechazarInscripcion = document.getElementById('btnRechazarInscripcion');
  const btnAdjuntarArchivo = document.getElementById('btnAdjuntarArchivo');
  
  // Aprobar documentos solo habilitado para estado 12 (En validación de documentos)
  if (btnAprobarDocumentos) {
    btnAprobarDocumentos.disabled = estadoId !== 12;
  }
  
  // Aprobar inscripción solo habilitado para estado 14 (Pendiente de aprobación final)
  if (btnAprobarInscripcion) {
    btnAprobarInscripcion.disabled = estadoId !== 14;
  }
  
  // Botón adjuntar archivo y leyenda solo visible para estado 12 (En validación de documentos)
  if (btnAdjuntarArchivo) {
    btnAdjuntarArchivo.style.display = estadoId === 12 ? 'inline-block' : 'none';
  }
  
  const filaArchivosAdicionales = document.getElementById('filaArchivosAdicionales');
  if (filaArchivosAdicionales) {
    const leyendaFormatos = filaArchivosAdicionales.querySelector('.form-text');
    if (leyendaFormatos) {
      leyendaFormatos.style.display = estadoId === 12 ? 'block' : 'none';
    }
  }
  
  if (btnRechazarInscripcion) btnRechazarInscripcion.disabled = false;
}

function limpiarDetalles() {
  const elementos = [
    'tipoEntidad', 'nitEntidad', 'correoNotificacion', 'paginaWeb', 'numeroTramite', 'selectEstado',
    'nombreRepresentante', 'numeroDocumento', 'correoRepresentante', 'telefonoRepresentante',
    'nombreResponsable', 'correoResponsable', 'telefonoResponsable'
  ];
  
  elementos.forEach(id => {
    const elemento = document.getElementById(id);
    if (elemento) elemento.textContent = id.includes('xxxxx') ? 'Información xxxxx' : '';
  });
  
  ['capitalSuscrito', 'valorPagado', 'fechaPago'].forEach(id => {
    const elemento = document.getElementById(id);
    if (elemento) elemento.textContent = 'Información xxxxx';
  });
  
  const tablaPagos = document.querySelector('#tablaPagos tbody');
  if (tablaPagos) tablaPagos.innerHTML = '';
  const tablaHistorial = document.querySelector('#tablaHistorial tbody');
  if (tablaHistorial) tablaHistorial.innerHTML = '<tr><td colspan="5" class="text-center text-muted">Seleccione una entidad para ver el historial</td></tr>';
  
  comprobantesPago = [];
  const btnAprobarDocumentos = document.getElementById('btnAprobarDocumentos');
  if (btnAprobarDocumentos) btnAprobarDocumentos.disabled = true;
  
  if (informacionEntidad) informacionEntidad.classList.add('d-none');
  if (detalleEntidad) detalleEntidad.classList.add('d-none');
}

function controlarEditabilidadInformacionGeneral() {
  const userName = localStorage.getItem('currentUser');
  const isAdmin = userName && userName.toLowerCase() === 'adminsief';
  const userPerfil = localStorage.getItem('userPerfil');
  
  // Mostrar botones de pago solo para perfil DOT y AdminSief
  const isDOTProfile = userPerfil === 'Profesional DOT';
  const showPaymentButtons = isAdmin || isDOTProfile;
  
  const btnAdjuntarComprobante = document.getElementById('btnAdjuntarComprobante');
  const btnConfirmarPago = document.getElementById('btnConfirmarPago');
  const btnCancelarPago = document.getElementById('btnCancelarPago');
  
  if (btnAdjuntarComprobante) btnAdjuntarComprobante.style.display = showPaymentButtons ? 'inline-block' : 'none';
  if (btnConfirmarPago) btnConfirmarPago.style.display = showPaymentButtons ? 'inline-block' : 'none';
  if (btnCancelarPago) btnCancelarPago.style.display = 'none';
  
  // Controles para Jefe SSD
  const isJefeSSD = userPerfil === 'Jefe SSD';
  
  if (!isAdmin && !isJefeSSD) {
    const btnAprobarInscripcion = document.getElementById('btnAprobarInscripcion');
    if (btnAprobarInscripcion) btnAprobarInscripcion.style.display = 'none';
  }
  
  if (!isAdmin && !isJefeSSD) {
    const btnModificarCapital = document.getElementById('btnModificarCapital');
    const filaArchivosAdicionales = document.getElementById('filaArchivosAdicionales');
    const filaArchivosAdicionalesPago = document.getElementById('filaArchivosAdicionalesPago');
    
    if (btnModificarCapital) btnModificarCapital.style.display = 'none';
    if (filaArchivosAdicionales) filaArchivosAdicionales.style.display = 'none';
    if (filaArchivosAdicionalesPago) filaArchivosAdicionalesPago.style.display = 'none';
  }
}

function initializeApp() {
  const userName = localStorage.getItem('currentUser');
  const funcionarioSpan = document.getElementById('nombreFuncionario');
  const departamentoSpan = document.getElementById('departamentoFuncionario');
  
  if (userName && funcionarioSpan) {
    const parts = userName.split('.');
    const displayUser = parts.map(p => p.charAt(0).toUpperCase() + p.slice(1)).join(' ');
    funcionarioSpan.textContent = displayUser;
    
    const userAreaName = localStorage.getItem('userAreaName');
    const userArea = localStorage.getItem('userArea');
    let tipoUsuario = 'Funcionario';
    let departamento = '';
    
    const userPerfil = localStorage.getItem('userPerfil');
    
    if (userName.toLowerCase() === 'adminsief') {
      tipoUsuario = 'Administrador SIEF';
    } else if (userPerfil) {
      tipoUsuario = userPerfil;
    } else if (userAreaName) {
      departamento = userAreaName;
    } else {
      const userRole = localStorage.getItem('userRole');
      switch(userArea) {
        case '52050': departamento = 'DOT'; break;
        case '52060': departamento = 'DIF'; break;
        case '52070': departamento = 'DGC'; break;
        case '59030': departamento = userRole === 'Jefe' ? 'Jefe SSD' : 'SSD'; break;
        default: departamento = userRole === 'Jefe' ? 'Jefe SSD' : 'SSD';
      }
    }
    
    if (departamentoSpan) {
      departamentoSpan.textContent = userPerfil ? tipoUsuario : (departamento ? `${tipoUsuario} ${departamento}` : tipoUsuario);
    }
  } else {
    window.location.href = '../index.html';
    return;
  }
  
  setupEventListeners();
  loadEntidadesGestionables();
}

function setupEventListeners() {
  const btnSalir = document.getElementById('btnSalir');
  if (btnSalir) {
    btnSalir.addEventListener('click', () => {
      localStorage.removeItem('currentUser');
      localStorage.removeItem('userArea');
      localStorage.removeItem('userAreaName');
      window.location.href = '../index.html';
    });
  }
  
  const btnAdjuntarArchivo = document.getElementById('btnAdjuntarArchivo');
  const uploadSection = document.getElementById('uploadSection');
  const btnSubirArchivo = document.getElementById('btnSubirArchivo');
  const btnCancelarSubida = document.getElementById('btnCancelarSubida');
  const documentosAdicionales = document.getElementById('documentosAdicionales');
  
  // Elementos para documentos adicionales de pago
  const btnAdjuntarArchivoPago = document.getElementById('btnAdjuntarArchivoPago');
  const uploadSectionPago = document.getElementById('uploadSectionPago');
  const btnSubirArchivoPago = document.getElementById('btnSubirArchivoPago');
  const btnCancelarSubidaPago = document.getElementById('btnCancelarSubidaPago');
  const documentosAdicionalesPago = document.getElementById('documentosAdicionalesPago');
  
  if (btnAdjuntarArchivo && uploadSection) {
    btnAdjuntarArchivo.addEventListener('click', () => {
      uploadSection.classList.remove('d-none');
      btnAdjuntarArchivo.classList.add('d-none');
    });
  }
  
  if (btnCancelarSubida && uploadSection && btnAdjuntarArchivo && documentosAdicionales) {
    btnCancelarSubida.addEventListener('click', () => {
      uploadSection.classList.add('d-none');
      btnAdjuntarArchivo.classList.remove('d-none');
      documentosAdicionales.value = '';
      const textSpan = document.getElementById('documentosAdicionalesText');
      if (textSpan) textSpan.textContent = 'Ningún archivo seleccionado';
    });
  }
  
  const documentosAdicionalesWrapper = document.getElementById('documentosAdicionalesWrapper');
  const documentosAdicionalesText = document.getElementById('documentosAdicionalesText');
  

  
  if (documentosAdicionalesWrapper && documentosAdicionales && documentosAdicionalesText) {
    documentosAdicionalesWrapper.addEventListener('click', () => {
      documentosAdicionales.click();
    });
    
    documentosAdicionales.addEventListener('change', () => {
      if (documentosAdicionales.files && documentosAdicionales.files.length > 0) {
        documentosAdicionalesText.textContent = documentosAdicionales.files[0].name;
        documentosAdicionalesText.className = 'text-dark flex-grow-1';
      } else {
        documentosAdicionalesText.textContent = 'Ningún archivo seleccionado';
        documentosAdicionalesText.className = 'text-muted flex-grow-1';
      }
    });
  }
  
  // Event listeners para documentos adicionales de pago
  if (btnAdjuntarArchivoPago && uploadSectionPago) {
    btnAdjuntarArchivoPago.addEventListener('click', () => {
      uploadSectionPago.classList.remove('d-none');
      btnAdjuntarArchivoPago.classList.add('d-none');
    });
  }
  
  if (btnCancelarSubidaPago && uploadSectionPago && btnAdjuntarArchivoPago && documentosAdicionalesPago) {
    btnCancelarSubidaPago.addEventListener('click', () => {
      uploadSectionPago.classList.add('d-none');
      btnAdjuntarArchivoPago.classList.remove('d-none');
      documentosAdicionalesPago.value = '';
      const textSpan = document.getElementById('documentosAdicionalesPagoText');
      if (textSpan) textSpan.textContent = 'Ningún archivo seleccionado';
    });
  }
  
  const documentosAdicionalesPagoWrapper = document.getElementById('documentosAdicionalesPagoWrapper');
  const documentosAdicionalesPagoText = document.getElementById('documentosAdicionalesPagoText');
  
  if (documentosAdicionalesPagoWrapper && documentosAdicionalesPago && documentosAdicionalesPagoText) {
    documentosAdicionalesPagoWrapper.addEventListener('click', () => {
      documentosAdicionalesPago.click();
    });
    
    documentosAdicionalesPago.addEventListener('change', () => {
      if (documentosAdicionalesPago.files && documentosAdicionalesPago.files.length > 0) {
        documentosAdicionalesPagoText.textContent = documentosAdicionalesPago.files[0].name;
        documentosAdicionalesPagoText.className = 'text-dark flex-grow-1';
      } else {
        documentosAdicionalesPagoText.textContent = 'Ningún archivo seleccionado';
        documentosAdicionalesPagoText.className = 'text-muted flex-grow-1';
      }
    });
  }
  
  if (btnSubirArchivoPago && documentosAdicionalesPago) {
    btnSubirArchivoPago.addEventListener('click', async () => {
      const archivo = documentosAdicionalesPago.files[0];
      if (!archivo) {
        Swal.fire('Error', 'Seleccione un archivo', 'error');
        return;
      }
      
      const { value: formValues } = await Swal.fire({
        title: 'Información del Pago Adicional',
        html: `
          <input id="swal-fecha" class="swal2-input" type="date" placeholder="Fecha de pago">
          <input id="swal-valor" class="swal2-input" type="number" step="0.01" placeholder="Valor">
        `,
        focusConfirm: false,
        showCancelButton: true,
        confirmButtonText: 'Continuar',
        cancelButtonText: 'Cancelar',
        preConfirm: () => {
          const fecha = document.getElementById('swal-fecha').value;
          const valor = document.getElementById('swal-valor').value;
          if (!fecha || !valor) {
            Swal.showValidationMessage('Fecha y valor son obligatorios');
            return false;
          }
          return { fecha, valor: parseFloat(valor) };
        }
      });
      
      if (formValues) {
        await subirArchivoAdicionalPago(archivo, formValues.fecha, formValues.valor);
      }
    });
  }
  
  if (btnSubirArchivo && documentosAdicionales) {
    btnSubirArchivo.addEventListener('click', async () => {
      const archivo = documentosAdicionales.files[0];
      if (!archivo) {
        Swal.fire('Error', 'Seleccione un archivo', 'error');
        return;
      }
      await subirArchivoAdicional(archivo);
    });
  }
  
  if (buscarEntidad) {
    buscarEntidad.addEventListener('input', (e) => {
      const texto = e.target.value;
      filtrarEntidades(texto);
      if (selectEntidad) {
        if (texto && entidadesData.length > 0) {
          selectEntidad.classList.remove('d-none');
        } else {
          selectEntidad.classList.add('d-none');
        }
      }
    });
    
    buscarEntidad.addEventListener('focus', () => {
      if (selectEntidad && entidadesData.length > 0) {
        selectEntidad.classList.remove('d-none');
      }
    });
  }
  
  if (selectEntidad) {
    selectEntidad.addEventListener('change', (e) => {
      const selectedOption = e.target.options[e.target.selectedIndex];
      if (selectedOption.value && buscarEntidad) {
        buscarEntidad.value = selectedOption.textContent;
        selectEntidad.classList.add('d-none');
        const entidadData = { 
          estadoNombre: selectedOption.dataset.estadoNombre || '',
          estadoId: parseInt(selectedOption.dataset.estadoId) || 0
        };
        cargarDetalleEntidad(selectedOption.value, entidadData);
      }
    });
  }
  
  document.addEventListener('click', (e) => {
    if (selectEntidad && !e.target.closest('.position-relative')) {
      selectEntidad.classList.add('d-none');
    }
  });
  
  const btnModificarCapital = document.getElementById('btnModificarCapital');
  if (btnModificarCapital) {
    btnModificarCapital.addEventListener('click', async () => {
      const entidadId = obtenerEntidadSeleccionadaId();
      if (!entidadId) {
        Swal.fire('Error', 'No hay entidad seleccionada', 'error');
        return;
      }
      
      const { value: formValues } = await Swal.fire({
        title: 'Modificar Capital Suscrito',
        html: `
          <input id="swal-input1" class="swal2-input" type="number" placeholder="Nuevo capital suscrito" step="0.01">
          <textarea id="swal-input2" class="swal2-textarea" placeholder="Observaciones (obligatorio)" rows="3"></textarea>
        `,
        focusConfirm: false,
        showCancelButton: true,
        confirmButtonText: 'Actualizar',
        cancelButtonText: 'Cancelar',
        preConfirm: () => {
          const capital = document.getElementById('swal-input1').value;
          const observaciones = document.getElementById('swal-input2').value;
          if (!capital || !observaciones.trim()) {
            Swal.showValidationMessage('Todos los campos son obligatorios');
            return false;
          }
          return { capital: parseFloat(capital), observaciones: observaciones.trim() };
        }
      });
      
      if (formValues) {
        try {
          const currentUser = localStorage.getItem('currentUser') || 'Usuario';
          const observacionCompleta = `El capital suscrito se cambia por: ${formValues.observaciones}`;
          const response = await fetch(`${API_BASE_URL}ActualizarCapitalSuscrito`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ 
              entidadId: parseInt(entidadId), 
              capitalSuscrito: formValues.capital,
              observaciones: observacionCompleta,
              usuario: currentUser
            })
          });
          
          if (response.ok) {
            Swal.fire('Éxito', 'Capital suscrito actualizado correctamente', 'success');
            cargarDetalleEntidad(entidadId);
          } else {
            const errorText = await response.text();
            Swal.fire('Error', errorText || 'No se pudo actualizar el capital', 'error');
          }
        } catch (error) {
          Swal.fire('Error', 'Error de conexión', 'error');
        }
      }
    });
  }
  
  const btnAprobarDocumentos = document.getElementById('btnAprobarDocumentos');
  if (btnAprobarDocumentos) {
    btnAprobarDocumentos.addEventListener('click', async () => {
      const entidadId = obtenerEntidadSeleccionadaId();
      if (!entidadId) {
        Swal.fire('Error', 'No hay entidad seleccionada', 'error');
        return;
      }
      
      const confirmResult = await Swal.fire({
        title: '¿Está seguro de aprobar los documentos?',
        showCancelButton: true,
        confirmButtonText: 'Sí',
        cancelButtonText: 'Cancelar'
      });
      
      if (!confirmResult.isConfirmed) return;
      
      try {
        const currentUser = localStorage.getItem('currentUser') || 'Usuario';
        const response = await fetch(`${API_BASE_URL}AprobarDocumentos`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ 
            entidadId: parseInt(entidadId),
            observaciones: 'Documentos aprobados correctamente',
            usuario: currentUser
          })
        });
        
        if (response.ok) {
          Swal.fire({
            title: 'Éxito',
            text: 'Documentos aprobados correctamente',
            icon: 'success',
            confirmButtonText: 'Cerrar'
          }).then(() => {
            location.reload();
          });
        } else {
          const errorText = await response.text();
          Swal.fire('Error', errorText || 'No se pudieron aprobar los documentos', 'error');
        }
      } catch (error) {
        Swal.fire('Error', 'Error de conexión', 'error');
      }
    });
  }
  
  const btnRechazarInscripcion = document.getElementById('btnRechazarInscripcion');
  if (btnRechazarInscripcion) {
    btnRechazarInscripcion.addEventListener('click', async () => {
      const entidadId = obtenerEntidadSeleccionadaId();
      if (!entidadId) {
        Swal.fire('Error', 'No hay entidad seleccionada', 'error');
        return;
      }
      
      const { value: observaciones } = await Swal.fire({
        title: 'Rechazar Inscripción',
        input: 'textarea',
        inputLabel: 'Motivo del rechazo (obligatorio)',
        inputPlaceholder: 'Ingrese el motivo del rechazo...',
        showCancelButton: true,
        confirmButtonText: 'Rechazar',
        cancelButtonText: 'Cancelar',
        inputValidator: (value) => {
          if (!value || !value.trim()) {
            return 'Debe ingresar el motivo del rechazo';
          }
        }
      });
      
      if (observaciones) {
        try {
          const currentUser = localStorage.getItem('currentUser') || 'Usuario';
          const response = await fetch(`${API_BASE_URL}RechazarInscripcion`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ 
              entidadId: parseInt(entidadId),
              observaciones: observaciones.trim(),
              usuario: currentUser
            })
          });
          
          if (response.ok) {
            Swal.fire('Éxito', 'Inscripción rechazada correctamente', 'success');
            // Resetear formulario
            if (buscarEntidad) buscarEntidad.value = '';
            if (selectEntidad) selectEntidad.classList.add('d-none');
            limpiarDetalles();
          } else {
            const errorText = await response.text();
            Swal.fire('Error', errorText || 'No se pudo rechazar la inscripción', 'error');
          }
        } catch (error) {
          Swal.fire('Error', 'Error de conexión', 'error');
        }
      }
    });
  }
  
  const botones = [
    { id: 'btnLimpiar', handler: () => {
      if (buscarEntidad) buscarEntidad.value = '';
      if (selectEntidad) selectEntidad.classList.add('d-none');
      limpiarDetalles();
    }},
    { id: 'btnCerrar', handler: () => {
      if (buscarEntidad) buscarEntidad.value = '';
      if (selectEntidad) selectEntidad.classList.add('d-none');
      limpiarDetalles();
      window.location.href = 'dashboard.html';
    }},
    { id: 'btnConfirmarPago', handler: confirmarPago },
    { id: 'btnAdjuntarComprobante', handler: async () => {
      const { value: formValues } = await Swal.fire({
        title: 'Adjuntar Comprobante de Pago',
        html: `
          <input id="swal-fecha" class="swal2-input" type="date" placeholder="Fecha de pago">
          <input id="swal-valor" class="swal2-input" type="number" step="0.01" placeholder="Valor">
          <div class="file-wrapper" id="swal-archivo-wrapper" style="display: flex; align-items: center; justify-content: space-between; border: 1px solid #dee2e6; border-radius: 0.375rem; padding: 0.375rem 0.75rem; background-color: #fff; cursor: pointer; min-height: 38px; margin: 0.5rem 0;">
            <span class="text-muted flex-grow-1" id="swal-archivo-text">Ningún archivo seleccionado</span>
            <span class="badge bg-light text-dark">Seleccionar archivo</span>
            <input id="swal-archivo" type="file" accept=".pdf" style="position: absolute; opacity: 0; width: 0; height: 0;">
          </div>
        `,
        didOpen: () => {
          const wrapper = document.getElementById('swal-archivo-wrapper');
          const fileInput = document.getElementById('swal-archivo');
          const textSpan = document.getElementById('swal-archivo-text');
          
          wrapper.addEventListener('click', () => fileInput.click());
          fileInput.addEventListener('change', () => {
            if (fileInput.files && fileInput.files.length > 0) {
              textSpan.textContent = fileInput.files[0].name;
              textSpan.className = 'text-dark flex-grow-1';
            } else {
              textSpan.textContent = 'Ningún archivo seleccionado';
              textSpan.className = 'text-muted flex-grow-1';
            }
          });
        },
        focusConfirm: false,
        showCancelButton: true,
        confirmButtonText: 'Subir',
        cancelButtonText: 'Cancelar',
        preConfirm: () => {
          const fecha = document.getElementById('swal-fecha').value;
          const valor = document.getElementById('swal-valor').value;
          const archivo = document.getElementById('swal-archivo').files[0];
          if (!fecha || !valor || !archivo) {
            Swal.showValidationMessage('Todos los campos son obligatorios');
            return false;
          }
          return { fecha, valor: parseFloat(valor), archivo };
        }
      });
      
      if (formValues) {
        try {
          Swal.fire({title: 'Subiendo comprobante...', allowOutsideClick: false, didOpen: () => Swal.showLoading()});
          
          const entidadId = obtenerEntidadSeleccionadaId();
          const fileBase64 = await convertirArchivoABase64(formValues.archivo);
          
          const response = await fetch(`${API_BASE_URL}SubirComprobantePago?entidadId=${entidadId}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              fechaPago: formValues.fecha,
              valor: formValues.valor,
              archivoBase64: fileBase64,
              nombreArchivo: formValues.archivo.name
            })
          });
          
          if (response.ok) {
            const tabla = document.querySelector('#tablaPagos tbody');
            const newRow = document.createElement('tr');
            const fecha = new Date(formValues.fecha).toLocaleDateString('es-CO');
            const valor = formatearMonedaConDecimales(formValues.valor);
            newRow.innerHTML = `<td>${fecha}</td><td>${valor}</td><td><a href="#" class="text-primary">Ver archivo</a></td>`;
            tabla.appendChild(newRow);
            
            Swal.fire('Éxito', 'Comprobante subido correctamente', 'success');
          } else {
            Swal.fire('Error', 'No se pudo subir el comprobante', 'error');
          }
        } catch (error) {
          Swal.fire('Error', 'Error de conexión', 'error');
        }
      }
    }}
  ];
  
  botones.forEach(({ id, handler }) => {
    const btn = document.getElementById(id);
    if (btn) btn.addEventListener('click', handler);
  });
}

async function subirArchivoAdicional(archivo) {
  try {
    Swal.fire({title: 'Subiendo archivo...', allowOutsideClick: false, didOpen: () => Swal.showLoading()});
    
    const entidadId = obtenerEntidadSeleccionadaId();
    if (!entidadId) {
      Swal.fire('Error', 'No hay entidad seleccionada', 'error');
      return;
    }
    
    const fileBase64 = await convertirArchivoABase64(archivo);
    const response = await fetch(`${API_BASE_URL}SubirDocumentoAdicional?entidadId=${entidadId}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        archivoBase64: fileBase64,
        nombreArchivo: archivo.name
      })
    });
    
    if (!response.ok) throw new Error('Error al subir archivo');
    
    const resultado = await response.json();
    agregarArchivoVisualmente(archivo.name, resultado.url);
    
    const uploadSection = document.getElementById('uploadSection');
    const btnAdjuntarArchivo = document.getElementById('btnAdjuntarArchivo');
    const documentosAdicionales = document.getElementById('documentosAdicionales');
    
    if (uploadSection) uploadSection.classList.add('d-none');
    if (btnAdjuntarArchivo) btnAdjuntarArchivo.classList.remove('d-none');
    if (documentosAdicionales) documentosAdicionales.value = '';
    
    Swal.fire({icon: 'success', title: 'Archivo subido', text: 'El archivo se ha agregado correctamente', timer: 2000, showConfirmButton: false});
  } catch (error) {
    Swal.fire('Error', 'No se pudo subir el archivo', 'error');
  }
}

function agregarArchivoVisualmente(nombreArchivo, url) {
  const tablaDocumentos = document.querySelector('#tablaDocumentosAdicionales tbody');
  if (!tablaDocumentos) return;
  
  const existingDocs = tablaDocumentos.children.length;
  const newRow = document.createElement('tr');
  const link = document.createElement('a');
  link.href = '#';
  link.className = 'text-primary';
  link.textContent = 'Ver archivo';
  link.addEventListener('click', (e) => {
    e.preventDefault();
    descargarArchivoDirecto(url);
  });
  
  const td1 = document.createElement('td');
  td1.className = 'fw-bold';
  td1.style.width = '40%';
  td1.textContent = `Documento [${existingDocs + 1}]:`;
  
  const td2 = document.createElement('td');
  td2.appendChild(link);
  
  newRow.appendChild(td1);
  newRow.appendChild(td2);
  tablaDocumentos.appendChild(newRow);
}

async function subirArchivoAdicionalPago(archivo, fecha, valor) {
  try {
    Swal.fire({title: 'Subiendo archivo...', allowOutsideClick: false, didOpen: () => Swal.showLoading()});
    
    const entidadId = obtenerEntidadSeleccionadaId();
    if (!entidadId) {
      Swal.fire('Error', 'No hay entidad seleccionada', 'error');
      return;
    }
    
    const fileBase64 = await convertirArchivoABase64(archivo);
    const response = await fetch(`${API_BASE_URL}SubirDocumentoAdicionalPago?entidadId=${entidadId}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        archivoBase64: fileBase64,
        nombreArchivo: archivo.name,
        fechaPago: fecha,
        valor: valor
      })
    });
    
    if (!response.ok) throw new Error('Error al subir archivo');
    
    const resultado = await response.json();
    agregarArchivoVisualmentePago(archivo.name, resultado.url);
    
    const uploadSectionPago = document.getElementById('uploadSectionPago');
    const btnAdjuntarArchivoPago = document.getElementById('btnAdjuntarArchivoPago');
    const documentosAdicionalesPago = document.getElementById('documentosAdicionalesPago');
    
    if (uploadSectionPago) uploadSectionPago.classList.add('d-none');
    if (btnAdjuntarArchivoPago) btnAdjuntarArchivoPago.classList.remove('d-none');
    if (documentosAdicionalesPago) documentosAdicionalesPago.value = '';
    
    Swal.fire({icon: 'success', title: 'Archivo subido', text: 'El archivo se ha agregado correctamente', timer: 2000, showConfirmButton: false});
  } catch (error) {
    Swal.fire('Error', 'No se pudo subir el archivo', 'error');
  }
}

function agregarArchivoVisualmentePago(nombreArchivo, url) {
  const tablaDocumentos = document.querySelector('#tablaDocumentosAdicionalesPago tbody');
  if (!tablaDocumentos) return;
  
  const existingDocs = tablaDocumentos.children.length;
  const newRow = document.createElement('tr');
  const link = document.createElement('a');
  link.href = '#';
  link.className = 'text-primary';
  link.textContent = 'Ver archivo';
  link.addEventListener('click', (e) => {
    e.preventDefault();
    descargarArchivoDirecto(url);
  });
  
  const td1 = document.createElement('td');
  td1.className = 'fw-bold';
  td1.style.width = '40%';
  td1.textContent = `Documento [${existingDocs + 1}]:`;
  
  const td2 = document.createElement('td');
  td2.appendChild(link);
  
  newRow.appendChild(td1);
  newRow.appendChild(td2);
  tablaDocumentos.appendChild(newRow);
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