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
