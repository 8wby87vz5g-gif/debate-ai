-- Execute once in your Supabase SQL editor.
create table if not exists public.waitlist (
  id uuid primary key default gen_random_uuid(),
  name text not null check (char_length(name) between 1 and 80),
  email text not null unique,
  handle text,
  role text not null,
  consent_at timestamptz not null,
  created_at timestamptz not null default now()
);
alter table public.waitlist enable row level security;
-- No public access policy: only the server-side service-role key can insert/read.
