# Dominio

Capa de modelos independientes de Angular, ASP.NET, EF Core y proveedores IA.
Iteración 1: Organization, AcademicGroup, GroupMembership, roles y aprobación
docente. AppUser y todo el almacenamiento Identity permanecen en Infrastructure.
Las reglas de acceso puras viven en Application/Identity y se prueban sin BD.
