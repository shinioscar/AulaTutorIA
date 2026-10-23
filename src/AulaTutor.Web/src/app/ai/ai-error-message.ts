// Safe local copy, never display upstream response bodies.
export function aiErrorMessage(code:string):string {
  return ({InvalidResponse:'La IA no logró completar una respuesta con el formato válido. Reintenta la misma solicitud o cancélala para ajustar la petición.',
    RateLimited:'Se alcanzó un límite de solicitudes, tokens o presupuesto. Espera antes de reintentar.',
    Authentication:'El proveedor rechazó la credencial configurada. Consulta al administrador.',
    Forbidden:'El proveedor no permite esta operación.',Configuration:'La configuración del proveedor requiere revisión.',
    Unsupported:'El modelo no admite el formato solicitado.',InvalidRequest:'La solicitud supera los límites o contiene un formato no admitido.',
    Timeout:'Se agotó el tiempo de la solicitud.',Connection:'No se pudo conectar con el proveedor.',
    Unavailable:'El proveedor no está disponible temporalmente.',Rejected:'El proveedor rechazó la solicitud.',
    Interrupted:'La generación quedó interrumpida. Puedes recuperar la misma solicitud.',Canceled:'La solicitud fue cancelada.'} as Record<string,string>)[code]??'La solicitud no se completó.';
}
