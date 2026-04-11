-- Categorías = familias del inventario del cliente (código en Excel hoja "codigo de familia").
-- Idempotente: ON CONFLICT por nombre único.
-- Fuente canónica duplicada en scripts/familias_cliente.json (mantener alineado).
--
--   psql "$DATABASE_URL" -f scripts/seed_categorias_cliente.sql

INSERT INTO "CategoriasProducto" ("Nombre", "Descripcion", "Activo", "FechaCreacion")
VALUES
  ('VARIEDADES M & G', NULL, TRUE, NOW()),
  ('SWEET PEA', NULL, TRUE, NOW()),
  ('SOFIA ACCESORIOS', NULL, TRUE, NOW()),
  ('RODRYS VARIEDADES', NULL, TRUE, NOW()),
  ('DUQUESA BOUTIQUE', NULL, TRUE, NOW()),
  ('DETALLES Y MAS', NULL, TRUE, NOW()),
  ('MIDENCE', NULL, TRUE, NOW()),
  ('DULCES LA CAPI', NULL, TRUE, NOW()),
  ('TIENDAS AJ', NULL, TRUE, NOW()),
  ('BONITAS', NULL, TRUE, NOW()),
  ('WELLNESS PLACE', NULL, TRUE, NOW()),
  ('SOULMATE', NULL, TRUE, NOW()),
  ('LA BOUTIQUE', NULL, TRUE, NOW()),
  ('VARIEDADES YUSTER', NULL, TRUE, NOW()),
  ('ISA BLUSH', NULL, TRUE, NOW()),
  ('KE ENCANTO', NULL, TRUE, NOW()),
  ('MUSE STORE', NULL, TRUE, NOW()),
  ('VARIEDADES THAEL', NULL, TRUE, NOW()),
  ('COQUETTE', NULL, TRUE, NOW()),
  ('AURALIS SHOP', NULL, TRUE, NOW()),
  ('MAN STYLE', NULL, TRUE, NOW()),
  ('MIDNIGTH', NULL, TRUE, NOW()),
  ('CURIOSIDADES', NULL, TRUE, NOW())
ON CONFLICT ("Nombre") DO UPDATE SET
  "Activo" = EXCLUDED."Activo";
