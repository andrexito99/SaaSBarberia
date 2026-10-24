/* ==========================================================================
   MÓDULO MULTITENANT: SOPORTE DE SLUG POR SEDE (TENANT)
   Permite URLs amigables de registro público y reservas (ej: /barberia/barberia-el-negro/Account/Register)
   ========================================================================== */

-- Para PostgreSQL (Supabase):
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_name = 'Tenants' AND column_name = 'Slug'
    ) THEN
        ALTER TABLE "Tenants" ADD COLUMN "Slug" VARCHAR(150) NULL;
        CREATE UNIQUE INDEX IF NOT EXISTS "UX_Tenants_Slug" ON "Tenants" ("Slug") WHERE "Slug" IS NOT NULL;
    END IF;
END $$;

-- Generar slug inicial para los tenants existentes basados en su subdominio si no lo tienen asignado:
UPDATE "Tenants"
SET "Slug" = LOWER(REGEXP_REPLACE(REPLACE("Subdominio", ' ', '-'), '[^a-zA-Z0-9\-]', '', 'g'))
WHERE "Slug" IS NULL AND "Subdominio" IS NOT NULL;
