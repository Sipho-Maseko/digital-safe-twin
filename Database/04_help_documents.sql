-- table for help guide pdf files uploaded by admin,,,
-- (the app also creates this automaticaly at startup via Global.asax)
create table if not exists help_documents (
  doc_id        uuid primary key default gen_random_uuid(),
  title         text not null,
  description   text,
  category      text not null default 'general',
  storage_key   text not null unique,
  original_name text,
  file_size     bigint,
  uploaded_by   uuid references users(user_id),
  created_at    timestamptz not null default now()
);

-- index so searching guides by title stays quick...
create index if not exists help_documents_title_idx on help_documents (lower(title));
