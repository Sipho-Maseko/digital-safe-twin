-- fixes hotspot cells so the cordinates are the cell CENTER not the corner,,,
-- (the app also applies this automaticaly at startup via Global.asax)
create or replace view hotspot_cells as
with grid as (
  select ST_SnapToGrid(ST_Transform(geom::geometry, 3857), 40) as cell_3857,
         category
  from incidents
  where invalid_flag = false
)
select ST_Y(ST_Transform(ST_Translate(cell_3857, 20, 20), 4326)) as cell_lat,
       ST_X(ST_Transform(ST_Translate(cell_3857, 20, 20), 4326)) as cell_lng,
       count(*)::int                        as incident_count,
       array_agg(distinct category)         as categories
from grid
group by cell_3857;
