# Willow appointment service

## Relational storage

Mira used PostgreSQL to store fictional clinic appointments. She designed tables for appointment slots and reservations, wrote SQL migrations, and added a unique constraint to prevent two reservations for one slot. A transaction kept each booking and its audit entry together.

## Query improvements

Mira inspected slow appointment queries and added an index on the scheduled date. She checked the query plan against a generated dataset. The document reports development work and makes no claim about database administration certification.
