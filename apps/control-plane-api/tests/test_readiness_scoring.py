"""Tests for readiness scoring functions."""

import pytest
from datetime import datetime, timezone, timedelta
from app.readiness.scoring import (
    clamp_score,
    readiness_label,
    qaqc_health_score,
    sync_freshness_score,
    weighted_readiness
)


def test_clamp_score():
    """Test that scores are clamped between 0 and 100."""
    # Test values below 0
    assert clamp_score(-5.0) == 0.0
    assert clamp_score(-100.0) == 0.0
    
    # Test values between 0 and 100
    assert clamp_score(0.0) == 0.0
    assert clamp_score(50.0) == 50.0
    assert clamp_score(100.0) == 100.0
    
    # Test values above 100
    assert clamp_score(105.0) == 100.0
    assert clamp_score(200.0) == 100.0
    
    # Test decimal rounding
    assert clamp_score(50.123456) == 50.12


def test_readiness_label():
    """Test that readiness labels are assigned correctly."""
    # Ready (90 and above)
    assert readiness_label(100.0) == "Ready"
    assert readiness_label(90.0) == "Ready"
    assert readiness_label(95.5) == "Ready"
    
    # On Track (75-89.99)
    assert readiness_label(89.99) == "On Track"
    assert readiness_label(75.0) == "On Track"
    assert readiness_label(79.5) == "On Track"
    
    # At Risk (60-74.99)
    assert readiness_label(74.99) == "At Risk"
    assert readiness_label(60.0) == "At Risk"
    assert readiness_label(65.5) == "At Risk"
    
    # Behind (40-59.99)
    assert readiness_label(59.99) == "Behind"
    assert readiness_label(40.0) == "Behind"
    assert readiness_label(45.5) == "Behind"
    
    # Critical (below 40)
    assert readiness_label(39.99) == "Critical"
    assert readiness_label(0.0) == "Critical"
    assert readiness_label(-5.0) == "Critical"


def test_qaqc_health_score():
    """Test QA/QC health score calculation."""
    # Test with zero elements: no model data must not yield a perfect score
    # (product truth: missing data is not equivalent to a perfect score).
    assert qaqc_health_score(0, 0, 0, 0, 0) == 0.0

    # Test with no issues
    assert qaqc_health_score(100, 0, 0, 0, 0) == 100.0
    
    # Test with issues - critical only
    assert qaqc_health_score(100, 1, 0, 0, 0) == 95.0  # 1 * 5 = 5 penalty
    
    # Test with issues - high only
    assert qaqc_health_score(100, 0, 1, 0, 0) == 98.0  # 1 * 2 = 2 penalty
    
    # Test with issues - medium only
    assert qaqc_health_score(100, 0, 0, 1, 0) == 99.25  # 1 * 0.75 = 0.75 penalty
    
    # Test with issues - low only
    assert qaqc_health_score(100, 0, 0, 0, 1) == 99.75  # 1 * 0.25 = 0.25 penalty
    
    # Test with mixed issues
    assert qaqc_health_score(100, 2, 1, 1, 1) == 87.0  # (2*5 + 1*2 + 1*0.75 + 1*0.25) = 13 penalty
    
    # Test with more elements
    assert qaqc_health_score(50, 1, 0, 0, 0) == 95.0  # 1 * 5 / (50/100) = 10 penalty = 90.0 + 5.0


def test_sync_freshness_score():
    """Test sync freshness score calculation."""
    # Test with None date
    assert sync_freshness_score(None) == 0.0
    
    # Test recent sync (within 1 day)
    now = datetime.now(timezone.utc)
    recent = now - timedelta(hours=12)
    assert sync_freshness_score(recent) == 100.0
    
    # Test sync 1-3 days old
    recent_2days = now - timedelta(days=2)
    assert sync_freshness_score(recent_2days) == 85.0
    
    # Test sync 3-7 days old
    recent_5days = now - timedelta(days=5)
    assert sync_freshness_score(recent_5days) == 70.0
    
    # Test sync 7-14 days old
    recent_10days = now - timedelta(days=10)
    assert sync_freshness_score(recent_10days) == 50.0
    
    # Test sync older than 14 days
    old = now - timedelta(days=20)
    assert sync_freshness_score(old) == 25.0


def test_weighted_readiness():
    """Test weighted readiness calculation."""
    # Test with all scores at 100
    assert weighted_readiness(100.0, 100.0, 100.0) == 100.0
    
    # Test with all scores at 0
    assert weighted_readiness(0.0, 0.0, 0.0) == 0.0
    
    # Test weighted average
    assert weighted_readiness(100.0, 50.0, 75.0) == 80.0  # 100*0.5 + 50*0.3 + 75*0.2 = 50 + 15 + 15 = 80
    assert weighted_readiness(80.0, 60.0, 40.0) == 66.0  # 80*0.5 + 60*0.3 + 40*0.2 = 40 + 18 + 8 = 66
    
    # Test with one score at 0
    assert weighted_readiness(0.0, 50.0, 50.0) == 25.0  # 0*0.5 + 50*0.3 + 50*0.2 = 0 + 15 + 10 = 25
    assert weighted_readiness(50.0, 0.0, 50.0) == 35.0  # 50*0.5 + 0*0.3 + 50*0.2 = 25 + 0 + 10 = 35
    assert weighted_readiness(50.0, 50.0, 0.0) == 40.0  # 50*0.5 + 50*0.3 + 0*0.2 = 25 + 15 + 0 = 40