-- sql/CosineSimilarity.sql
CREATE OR REPLACE FUNCTION public.cosine_similarity(vec vector, arr real[])
RETURNS double precision AS $$
DECLARE
    dot double precision;
    norm_vec double precision;
    norm_arr double precision;
BEGIN
    SELECT SUM(val1 * val2) INTO dot
    FROM (
        SELECT unnest(string_to_array(trim(both '[]' from vec::text), ','))::double precision AS val1,
               unnest(arr)::double precision AS val2
    ) s;
    
    SELECT sqrt(SUM(val1 * val1)) INTO norm_vec
    FROM (
        SELECT unnest(string_to_array(trim(both '[]' from vec::text), ','))::double precision AS val1
    ) s;
    
    SELECT sqrt(SUM(val2 * val2)) INTO norm_arr
    FROM (
        SELECT unnest(arr)::double precision AS val2
    ) s;
    
    IF norm_vec = 0 OR norm_arr = 0 THEN
         RETURN 0;
    END IF;
    
    RETURN dot / (norm_vec * norm_arr);
END;
$$ LANGUAGE plpgsql IMMUTABLE;