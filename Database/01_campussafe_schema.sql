-- enables postgis spatial tools,,,
create extension if not exists postgis;

-- enables random uuid generator...
create extension if not exists pgcrypto;

-- allowed roles for users in system,,,
create type user_role as enum ('student', 'guard', 'supervisor');

-- categories of incidents you can report...
create type incident_category as enum ('theft','assault','harassment','suspicious_activity','vandalism','medical','other');

-- workflow states for incidents,,,
create type incident_status as enum ('New','Acknowledged','In Progress','Resolved');

-- dispatch stages for security guards...
create type dispatch_status as enum ('pending','assigned','en_route','on_scene','resolved','cancelled');

-- types of attachments on reports,,,
create type attachment_kind as enum ('photo','note','other');

-- kinds of notification messages sent out...
create type notification_kind as enum ('sos','dispatch','status_update','system');

-- user acounts table for students and officers,,,
create table users (
  user_id        uuid primary key default gen_random_uuid(),
  email          text unique not null,
  full_name      text not null,
  role           user_role not null default 'student',
  phone          text,
  student_number text,
  badge_number   text,
  is_active      boolean not null default true,
  created_at     timestamptz not null default now()
);

-- campus landmarks and buildings with coords...
create table campus_locations (
  location_id uuid primary key default gen_random_uuid(),
  name        text not null,
  kind        text,
  description text,
  latitude    double precision not null,
  longitude   double precision not null,
  geom        geography(Point,4326)
              generated always as (ST_SetSRID(ST_MakePoint(longitude, latitude),4326)::geography) stored
);

-- spatial index for landmark points,,,
create index campus_locations_geom_idx on campus_locations using gist (geom);

-- walking path points for route finding...
create table path_nodes (
  node_id     uuid primary key default gen_random_uuid(),
  name        text,
  location_id uuid references campus_locations(location_id),
  latitude    double precision not null,
  longitude   double precision not null,
  geom        geography(Point,4326)
              generated always as (ST_SetSRID(ST_MakePoint(longitude, latitude),4326)::geography) stored
);

-- spatial index for route nodes,,,
create index path_nodes_geom_idx on path_nodes using gist (geom);

-- connections between path nodes with walking distance...
create table path_links (
  link_id          uuid primary key default gen_random_uuid(),
  from_node        uuid not null references path_nodes(node_id),
  to_node          uuid not null references path_nodes(node_id),
  length_metres    double precision not null,
  base_risk_weight double precision not null default 0,
  is_lit           boolean default true,
  is_indoor        boolean default false,
  check (from_node <> to_node)
);

-- index on start node of path,,,
create index path_links_from_idx on path_links(from_node);

-- index on end node of path...
create index path_links_to_idx on path_links(to_node);

-- keeps track of generated demo batches,,,
create table seed_records (
  seed_id      uuid primary key default gen_random_uuid(),
  name         text not null,
  seed_value   integer,
  notes        text,
  generated_at timestamptz not null default now()
);

-- incident reports and sos alerts logged on map...
create table incidents (
  incident_id  uuid primary key default gen_random_uuid(),
  reporter_id  uuid references users(user_id),
  category     incident_category not null,
  description  text,
  status       incident_status not null default 'New',
  is_emergency boolean not null default false,
  latitude     double precision not null,
  longitude    double precision not null,
  geom         geography(Point,4326)
               generated always as (ST_SetSRID(ST_MakePoint(longitude, latitude),4326)::geography) stored,
  occurred_at  timestamptz,
  reported_at  timestamptz not null default now(),
  invalid_flag boolean not null default false,
  seed_id      uuid references seed_records(seed_id)
);

-- spatial index on incident coordinates,,,
create index incidents_geom_idx     on incidents using gist (geom);

-- index on incident status for fast filtering...
create index incidents_status_idx   on incidents(status);

-- index on reporter user id,,,
create index incidents_reporter_idx on incidents(reporter_id);

-- index on seed batch id...
create index incidents_seed_idx     on incidents(seed_id);

-- photos and notes attached to incidents,,,
create table incident_attachments (
  attachment_id uuid primary key default gen_random_uuid(),
  incident_id   uuid not null references incidents(incident_id) on delete cascade,
  kind          attachment_kind not null default 'photo',
  storage_key   text,
  note          text,
  uploaded_at   timestamptz not null default now()
);

-- index for finding attachments of an incident...
create index incident_attachments_incident_idx on incident_attachments(incident_id);

-- history log of status updates on reports,,,
create table status_history (
  history_id  uuid primary key default gen_random_uuid(),
  incident_id uuid not null references incidents(incident_id) on delete cascade,
  changed_by  uuid references users(user_id),
  old_status  incident_status,
  new_status  incident_status not null,
  note        text,
  changed_at  timestamptz not null default now()
);

-- index for looking up status history of an incident...
create index status_history_incident_idx on status_history(incident_id);

-- security officer dispatch assignments for incidents,,,
create table dispatches (
  dispatch_id     uuid primary key default gen_random_uuid(),
  incident_id     uuid not null references incidents(incident_id) on delete cascade,
  guard_id        uuid references users(user_id),
  dispatched_by   uuid references users(user_id),
  status          dispatch_status not null default 'pending',
  dispatched_at   timestamptz not null default now(),
  acknowledged_at timestamptz,
  arrived_at      timestamptz,
  closed_at       timestamptz,
  notes           text
);

-- index on dispatched incident id...
create index dispatches_incident_idx on dispatches(incident_id);

-- index on assigned guard id,,,
create index dispatches_guard_idx    on dispatches(guard_id);

-- stores latest known gps cordinates of users...
create table user_last_location (
  user_id    uuid primary key references users(user_id) on delete cascade,
  latitude   double precision not null,
  longitude  double precision not null,
  geom       geography(Point,4326)
             generated always as (ST_SetSRID(ST_MakePoint(longitude, latitude),4326)::geography) stored,
  accuracy_m double precision,
  is_on_duty boolean not null default false,
  updated_at timestamptz not null default now()
);

-- spatial index on user locations,,,
create index user_last_location_geom_idx on user_last_location using gist (geom);

-- routes saved by students for quick navigation...
create table saved_routes (
  route_id         uuid primary key default gen_random_uuid(),
  user_id          uuid not null references users(user_id) on delete cascade,
  label            text,
  from_location_id uuid references campus_locations(location_id),
  to_location_id   uuid references campus_locations(location_id),
  node_path        jsonb,
  length_metres    double precision,
  created_at       timestamptz not null default now()
);

-- index to load saved routes for a user,,,
create index saved_routes_user_idx on saved_routes(user_id);

-- notifications sent to users about alerts and dispatches...
create table notifications (
  notification_id uuid primary key default gen_random_uuid(),
  recipient_id    uuid not null references users(user_id) on delete cascade,
  kind            notification_kind not null,
  title           text not null,
  body            text,
  incident_id     uuid references incidents(incident_id) on delete cascade,
  dispatch_id     uuid references dispatches(dispatch_id) on delete cascade,
  is_read         boolean not null default false,
  created_at      timestamptz not null default now()
);

-- index on recipient and read status,,,
create index notifications_recipient_idx on notifications(recipient_id, is_read);

-- danger zones marked by campus security on map...
create table flagged_zones (
  zone_id    uuid primary key default gen_random_uuid(),
  created_by uuid references users(user_id),
  name       text,
  reason     text,
  risk_level smallint not null default 1,
  latitude   double precision not null,
  longitude  double precision not null,
  radius_m   double precision not null default 50,
  geom       geography(Point,4326)
             generated always as (ST_SetSRID(ST_MakePoint(longitude, latitude),4326)::geography) stored,
  is_active  boolean not null default true,
  created_at timestamptz not null default now()
);

-- spatial index for danger zone circles,,,
create index flagged_zones_geom_idx on flagged_zones using gist (geom);

-- groups nearby incidents into grid cells for heatmap...
create or replace view hotspot_cells as
with grid as (
  select ST_SnapToGrid(ST_Transform(geom::geometry, 3857), 40) as cell_3857,
         category
  from incidents
  where invalid_flag = false
)
select ST_Y(ST_Transform(cell_3857, 4326)) as cell_lat,
       ST_X(ST_Transform(cell_3857, 4326)) as cell_lng,
       count(*)::int                        as incident_count,
       array_agg(distinct category)         as categories
from grid
group by cell_3857;

-- finds the on duty guard closest to given cordinates,,,
create or replace function nearest_on_duty_guard(p_lat double precision, p_lng double precision)
returns table(guard_id uuid, full_name text, distance_m double precision)
language sql stable as $$
  select u.user_id, u.full_name,
         ST_Distance(l.geom, ST_SetSRID(ST_MakePoint(p_lng, p_lat),4326)::geography)
  from user_last_location l
  join users u on u.user_id = l.user_id
  where u.role in ('guard','supervisor') and l.is_on_duty = true
  order by l.geom <-> ST_SetSRID(ST_MakePoint(p_lng, p_lat),4326)::geography
  limit 1;
$$;

-- makes sure status only moves forward one step at a time...
create or replace function enforce_status_flow() returns trigger language plpgsql as $$
declare
  flow text[] := array['New','Acknowledged','In Progress','Resolved'];
  pos_old int := array_position(flow, OLD.status::text);
  pos_new int := array_position(flow, NEW.status::text);
  actor uuid := nullif(current_setting('app.user_id', true), '')::uuid;
begin
  if NEW.status = OLD.status then
    return NEW;
  end if;
  if pos_new <> pos_old + 1 then
    raise exception 'Illegal status change % -> %. Allowed: New -> Acknowledged -> In Progress -> Resolved (one step at a time).',
      OLD.status, NEW.status;
  end if;
  insert into status_history(incident_id, changed_by, old_status, new_status)
  values (NEW.incident_id, actor, OLD.status, NEW.status);
  return NEW;
end $$;

-- runs status check before incident update saves,,,
create trigger trg_enforce_status_flow
before update of status on incidents
for each row execute function enforce_status_flow();