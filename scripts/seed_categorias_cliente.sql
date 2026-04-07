-- Categorías de producto (solo nombre; el Id lo asigna la base de datos).
-- Idempotente: si el nombre ya existe (índice único), no duplica.
--
--   psql "$DATABASE_URL" -f scripts/seed_categorias_cliente.sql

INSERT INTO "CategoriasProducto" ("Nombre", "Descripcion", "Activo", "FechaCreacion")
VALUES
  ('VARIEDADES M & G', NULL, TRUE, NOW()),
  ('SWEET PEA', NULL, TRUE, NOW()),
  ('SOFIA ACCESORIOS', NULL, TRUE, NOW()),
  ('RODRYS VARIEDADES', NULL, TRUE, NOW()),
  ('MIDENCE', NULL, TRUE, NOW()),
  ('TIENDAS AJ', NULL, TRUE, NOW()),
  ('WELLNESS PLACE', NULL, TRUE, NOW()),
  ('SOULMATE', NULL, TRUE, NOW()),
  ('LA BOUTIQUE', NULL, TRUE, NOW()),
  ('VARIEDADES YUSTER', NULL, TRUE, NOW()),
  ('MUSE STORE', NULL, TRUE, NOW()),
  ('VARIEDADES THAEL', NULL, TRUE, NOW()),
  ('MAN STYLE', NULL, TRUE, NOW()),
  ('MIDNIGTH', NULL, TRUE, NOW())
ON CONFLICT ("Nombre") DO UPDATE SET
  "Activo" = EXCLUDED."Activo";
