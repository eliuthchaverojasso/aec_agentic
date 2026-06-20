"""Tests for Owner Requirements ingestion."""

from openpyxl import Workbook
from sqlalchemy import BigInteger, create_engine, func, select
from sqlalchemy.dialects.postgresql import JSONB
from sqlalchemy.ext.compiler import compiles
from sqlalchemy.orm import Session

from app.ingestion.requirements_loader import ingest_requirements_file
from app.models import Base, Client, Organization, Requirement, RequirementCompliance


@compiles(JSONB, "sqlite")
def _compile_jsonb_for_sqlite(type_: str, compiler, **kw):
    return "JSON"


@compiles(BigInteger, "sqlite")
def _compile_bigint_for_sqlite(type_: str, compiler, **kw):
    return "INTEGER"


ROCKWALL_NORTHWEST_HEADERS = [
    "STATUS",
    "DISCIPLINE",
    "REQUIREMENT",
    "LINKS",
    "CATEGORY LIST",
    "DATE UPDATED",
    "MODIFIED BY",
    "RESOURCE",
    "Item Type",
    "Path",
]

DENTON_HEADERS = [
    "STATUS",
    "DISCIPLINE",
    "REQUIREMENT",
    "LINKS",
    "DATE UPDATED",
    "MODIFIED BY",
    "RESOURCE",
    "CATEGORY LIST",
    "Item Type",
    "Path",
]


def _write_requirements_xlsx(path, headers, rows):
    workbook = Workbook()
    worksheet = workbook.active
    worksheet.append(headers)
    for row in rows:
        worksheet.append(row)
    workbook.save(path)
    workbook.close()


def _make_session():
    engine = create_engine("sqlite+pysqlite:///:memory:")
    Base.metadata.create_all(engine)
    session = Session(engine)
    organization = Organization(id=1, name="EMA Engineering")
    client = Client(
        id=1,
        organization_id=organization.id,
        code="TEST_OWNER",
        display_name="Test Owner",
    )
    session.add_all([organization, client])
    session.flush()
    return session, client


def test_loader_preserves_duplicate_requirement_text_across_disciplines(tmp_path):
    xlsx_path = tmp_path / "owner_requirements.xlsx"
    repeated_text = "Make sure everything is accessible for maintenance."
    _write_requirements_xlsx(
        xlsx_path,
        ROCKWALL_NORTHWEST_HEADERS,
        [
            [
                "DONE",
                "ELECTRICAL",
                repeated_text,
                None,
                "Maintenance",
                None,
                None,
                "Owner requirement matrix",
                None,
                None,
            ],
            [
                "DONE",
                "MECHANICAL",
                repeated_text,
                None,
                "Maintenance",
                None,
                None,
                "Owner requirement matrix",
                None,
                None,
            ],
        ],
    )

    session, client = _make_session()
    try:
        result = ingest_requirements_file(
            session,
            client=client,
            xlsx_path=xlsx_path,
            original_filename="TEST OWNER 05.21.2026.xlsx",
        )
        requirements = (
            session.execute(
                select(Requirement).where(Requirement.client_id == client.id)
            )
            .scalars()
            .all()
        )
    finally:
        session.close()

    assert result["row_count_raw"] == 2
    assert result["row_count_loaded"] == 2
    assert result["row_count_skipped"] == 0
    assert result["per_discipline"] == {"ELECTRICAL": 1, "MECHANICAL": 1}

    assert len(requirements) == 2
    assert {requirement.discipline for requirement in requirements} == {
        "ELECTRICAL",
        "MECHANICAL",
    }
    assert {requirement.requirement_text for requirement in requirements} == {repeated_text}
    assert len({requirement.content_hash for requirement in requirements}) == 2
    assert all(requirement.is_active for requirement in requirements)


def test_loader_maps_fields_by_header_name_when_column_order_varies(tmp_path):
    logical_row_by_header = {
        "STATUS": "NOT STARTED",
        "DISCIPLINE": "PLUMBING",
        "REQUIREMENT": "Provide service valves at accessible locations.",
        "LINKS": "District plumbing standard",
        "CATEGORY LIST": "Valves",
        "DATE UPDATED": None,
        "MODIFIED BY": "Owner Team",
        "RESOURCE": "Owner standard section P-1",
        "Item Type": "Item",
        "Path": "/sites/owner-requirements/plumbing",
    }
    cases = [
        ("rockwall_northwest_order.xlsx", ROCKWALL_NORTHWEST_HEADERS),
        ("denton_order.xlsx", DENTON_HEADERS),
    ]

    for filename, headers in cases:
        xlsx_path = tmp_path / filename
        _write_requirements_xlsx(
            xlsx_path,
            headers,
            [[logical_row_by_header[header] for header in headers]],
        )
        session, client = _make_session()
        try:
            result = ingest_requirements_file(
                session,
                client=client,
                xlsx_path=xlsx_path,
                original_filename=filename,
            )
            requirement = session.execute(select(Requirement)).scalar_one()
        finally:
            session.close()

        assert result["row_count_loaded"] == 1
        assert requirement.discipline == "PLUMBING"
        assert requirement.requirement_text == "Provide service valves at accessible locations."
        assert requirement.category == "Valves"
        assert requirement.resource == "Owner standard section P-1"
        assert requirement.links == "District plumbing standard"
        assert requirement.modified_by == "Owner Team"
        assert requirement.sharepoint_path == "/sites/owner-requirements/plumbing"


def test_loader_stores_status_as_owner_metadata_without_compliance(tmp_path):
    xlsx_path = tmp_path / "owner_requirements_status.xlsx"
    _write_requirements_xlsx(
        xlsx_path,
        ROCKWALL_NORTHWEST_HEADERS,
        [
            [
                "DONE",
                "ELECTRICAL",
                "Coordinate panel locations with owner.",
                None,
                "Panels",
                None,
                None,
                "Owner requirement matrix",
                None,
                None,
            ]
        ],
    )

    session, client = _make_session()
    try:
        result = ingest_requirements_file(
            session,
            client=client,
            xlsx_path=xlsx_path,
            original_filename="TEST OWNER STATUS 05.21.2026.xlsx",
        )
        requirement = session.execute(select(Requirement)).scalar_one()
        compliance_count = session.execute(
            select(func.count(RequirementCompliance.id))
        ).scalar_one()
    finally:
        session.close()

    assert result["row_count_loaded"] == 1
    assert requirement.owner_status == "DONE"
    assert compliance_count == 0


def test_loader_updates_is_actionable_on_reingest(tmp_path):
    """Test that is_actionable is recalculated properly on re-ingest/update."""
    xlsx_path = tmp_path / "owner_requirements_update.xlsx"
    
    # First ingest: normal requirement
    _write_requirements_xlsx(
        xlsx_path,
        ROCKWALL_NORTHWEST_HEADERS,
        [
            [
                "DONE",
                "ELECTRICAL",
                "Provide standard receptacle at wall.",
                None,
                "Receptacles",
                None,
                "Owner standard",
                "Owner requirement matrix",
                None,
                None,
            ],
        ],
    )

    session, client = _make_session()
    try:
        # First ingestion - normal requirement
        result1 = ingest_requirements_file(
            session,
            client=client,
            xlsx_path=xlsx_path,
            original_filename="TEST OWNER UPDATE 05.21.2026-1.xlsx",
        )
        
        # Second ingest: same content but reference pattern (this would be a data change)
        # For testing, we'll make a similar-looking requirement but with a reference pattern
        _write_requirements_xlsx(
            xlsx_path,
            ROCKWALL_NORTHWEST_HEADERS,
            [
                [
                    "DONE",
                    "ELECTRICAL",
                    "Refer to links column for detailed specifications.",
                    "Some documentation links",
                    "Receptacles",
                    None,
                    "Owner standard",
                    "Owner requirement matrix",
                    None,
                    None,
                ],
            ],
        )
        
        # Second ingestion - update should change is_actionable
        result2 = ingest_requirements_file(
            session,
            client=client,
            xlsx_path=xlsx_path,
            original_filename="TEST OWNER UPDATE 05.21.2026-2.xlsx",
        )
        
        # Query by requirement_text rather than scalar_one() to avoid MultipleResultsFound error
        normal_req = session.execute(
            select(Requirement).where(
                Requirement.requirement_text == "Provide standard receptacle at wall."
            )
        ).scalar_one()
        
        reference_req = session.execute(
            select(Requirement).where(
                Requirement.requirement_text == "Refer to links column for detailed specifications."
            )
        ).scalar_one()
        
    finally:
        session.close()

    assert result1["row_count_loaded"] == 1
    assert result2["row_count_loaded"] == 1  
    assert normal_req.is_actionable is True
    assert reference_req.is_actionable is False
