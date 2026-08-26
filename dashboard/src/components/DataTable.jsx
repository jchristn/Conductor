import React, { useState, useMemo, useRef, useEffect } from 'react';

const INTERACTIVE_ROW_CLICK_SELECTOR = [
  'button',
  'a',
  'input',
  'select',
  'textarea',
  'label',
  'summary',
  '[role="button"]',
  '[data-row-click-ignore="true"]'
].join(', ');

function DataTable({
  data = [],
  columns = [],
  loading = false,
  pageSize: defaultPageSize = 10,
  onRowClick = null,
  hidePagination = false
}) {
  const [currentPage, setCurrentPage] = useState(0);
  const [pageSize, setPageSize] = useState(defaultPageSize);
  const [sortConfig, setSortConfig] = useState({ key: null, direction: 'asc' });
  const [filters, setFilters] = useState({});
  const [pageInput, setPageInput] = useState('1');
  const [hiddenColumns, setHiddenColumns] = useState(() => new Set());
  const [columnsMenuOpen, setColumnsMenuOpen] = useState(false);
  const columnsMenuRef = useRef(null);

  // Columns the user is allowed to hide (action columns always stay visible so row actions
  // remain reachable; a column can opt out with `selectable: false`).
  const selectableColumns = useMemo(
    () => columns.filter((col) => !col.isAction && col.selectable !== false),
    [columns]
  );

  const visibleColumns = useMemo(
    () => columns.filter((col) => !hiddenColumns.has(col.key)),
    [columns, hiddenColumns]
  );

  useEffect(() => {
    if (!columnsMenuOpen) return undefined;
    const handleClickOutside = (event) => {
      if (columnsMenuRef.current && !columnsMenuRef.current.contains(event.target)) {
        setColumnsMenuOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [columnsMenuOpen]);

  const toggleColumn = (key) => {
    setHiddenColumns((prev) => {
      const next = new Set(prev);
      if (next.has(key)) {
        next.delete(key);
      } else {
        // Keep at least one selectable column visible.
        const remainingVisible = selectableColumns.filter((col) => !next.has(col.key) && col.key !== key);
        if (remainingVisible.length === 0) return prev;
        next.add(key);
      }
      return next;
    });
  };

  const showAllColumns = () => setHiddenColumns(new Set());

  const filteredAndSortedData = useMemo(() => {
    let result = [...data];

    // Apply filters
    Object.keys(filters).forEach((key) => {
      const filterValue = filters[key]?.toLowerCase();
      if (filterValue) {
        const column = columns.find((col) => col.key === key);
        result = result.filter((item) => {
          const value = column?.filterValue
            ? column.filterValue(item)
            : item[key];
          return String(value || '').toLowerCase().includes(filterValue);
        });
      }
    });

    // Apply sorting
    if (sortConfig.key) {
      const column = columns.find((col) => col.key === sortConfig.key);
      result.sort((a, b) => {
        const aValue = column?.sortValue ? column.sortValue(a) : a[sortConfig.key];
        const bValue = column?.sortValue ? column.sortValue(b) : b[sortConfig.key];

        if (aValue === null || aValue === undefined) return 1;
        if (bValue === null || bValue === undefined) return -1;

        const comparison = String(aValue).localeCompare(String(bValue), undefined, { numeric: true });
        return sortConfig.direction === 'asc' ? comparison : -comparison;
      });
    }

    return result;
  }, [data, columns, sortConfig, filters]);

  const totalPages = Math.max(1, Math.ceil(filteredAndSortedData.length / pageSize));
  const startIndex = currentPage * pageSize;
  const endIndex = Math.min(startIndex + pageSize, filteredAndSortedData.length);
  const paginatedData = filteredAndSortedData.slice(startIndex, endIndex);

  // Reset to valid page if current is out of bounds
  useMemo(() => {
    if (currentPage >= totalPages && totalPages > 0) {
      setCurrentPage(totalPages - 1);
      setPageInput(String(totalPages));
    }
  }, [currentPage, totalPages]);

  const handleSort = (key) => {
    const column = columns.find((col) => col.key === key);
    if (column?.sortable === false) return;

    setSortConfig((prev) => ({
      key,
      direction: prev.key === key && prev.direction === 'asc' ? 'desc' : 'asc'
    }));
  };

  const handleFilterChange = (key, value) => {
    setFilters((prev) => ({ ...prev, [key]: value }));
    setCurrentPage(0);
    setPageInput('1');
  };

  const goToPage = (page) => {
    const validPage = Math.max(0, Math.min(page, totalPages - 1));
    setCurrentPage(validPage);
    setPageInput(String(validPage + 1));
  };

  const handlePageInputChange = (e) => {
    setPageInput(e.target.value);
  };

  const handlePageInputSubmit = (e) => {
    if (e.key === 'Enter') {
      const pageNum = parseInt(pageInput, 10);
      if (!isNaN(pageNum) && pageNum >= 1 && pageNum <= totalPages) {
        goToPage(pageNum - 1);
      } else {
        setPageInput(String(currentPage + 1));
      }
    }
  };

  const getSortIcon = (key) => {
    if (sortConfig.key !== key) {
      return (
        <svg width="12" height="12" viewBox="0 0 12 12" fill="currentColor" opacity="0.3">
          <path d="M6 2L9 5H3L6 2Z" />
          <path d="M6 10L3 7H9L6 10Z" />
        </svg>
      );
    }
    return sortConfig.direction === 'asc' ? (
      <svg width="12" height="12" viewBox="0 0 12 12" fill="currentColor">
        <path d="M6 2L9 5H3L6 2Z" />
      </svg>
    ) : (
      <svg width="12" height="12" viewBox="0 0 12 12" fill="currentColor">
        <path d="M6 10L3 7H9L6 10Z" />
      </svg>
    );
  };

  const handleRowClick = (event, item) => {
    if (!onRowClick) {
      return;
    }

    const clickTarget = event.target instanceof Element ? event.target : event.currentTarget;
    if (clickTarget.closest(INTERACTIVE_ROW_CLICK_SELECTOR)) {
      return;
    }

    onRowClick(item);
  };

  if (loading) {
    return (
      <div className="data-table-loading">
        <div className="spinner"></div>
        <span>Loading...</span>
      </div>
    );
  }

  const hiddenCount = selectableColumns.filter((col) => hiddenColumns.has(col.key)).length;

  return (
    <div className="data-table-wrapper">
      <div className="data-table-toolbar">
        {selectableColumns.length > 0 && (
          <div className="column-selector" ref={columnsMenuRef}>
            <button
              type="button"
              className="column-selector-trigger"
              onClick={() => setColumnsMenuOpen((open) => !open)}
              aria-haspopup="true"
              aria-expanded={columnsMenuOpen}
              title="Choose which columns to display"
            >
              <svg width="14" height="14" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5">
                <rect x="1.5" y="2.5" width="13" height="11" rx="1.5" />
                <line x1="6" y1="2.5" x2="6" y2="13.5" />
                <line x1="10.5" y1="2.5" x2="10.5" y2="13.5" />
              </svg>
              Columns{hiddenCount > 0 ? ` (${selectableColumns.length - hiddenCount}/${selectableColumns.length})` : ''}
            </button>
            {columnsMenuOpen && (
              <div className="column-selector-dropdown">
                <div className="column-selector-header">
                  <span>Show columns</span>
                  <button type="button" className="column-selector-reset" onClick={showAllColumns} disabled={hiddenCount === 0}>
                    Reset
                  </button>
                </div>
                <div className="column-selector-list">
                  {selectableColumns.map((col) => (
                    <label key={col.key} className="column-selector-item">
                      <input
                        type="checkbox"
                        checked={!hiddenColumns.has(col.key)}
                        onChange={() => toggleColumn(col.key)}
                      />
                      <span>{typeof col.label === 'string' && col.label ? col.label : col.key}</span>
                    </label>
                  ))}
                </div>
              </div>
            )}
          </div>
        )}
      </div>

      <div className="data-table-container">
        {!hidePagination && (
          <div className="pagination">
            <div className="pagination-info">
              Showing {filteredAndSortedData.length === 0 ? 0 : startIndex + 1} to{' '}
              {endIndex} of {filteredAndSortedData.length} entries
            </div>

            <div className="pagination-controls">
              <select
                value={pageSize}
                onChange={(e) => {
                  setPageSize(Number(e.target.value));
                  setCurrentPage(0);
                  setPageInput('1');
                }}
              >
                <option value={10}>10</option>
                <option value={25}>25</option>
                <option value={50}>50</option>
                <option value={100}>100</option>
              </select>

              <button onClick={() => goToPage(0)} disabled={currentPage === 0}>
                First
              </button>
              <button onClick={() => goToPage(currentPage - 1)} disabled={currentPage === 0}>
                Prev
              </button>

              <span className="page-input-container">
                Page{' '}
                <input
                  type="text"
                  value={pageInput}
                  onChange={handlePageInputChange}
                  onKeyDown={handlePageInputSubmit}
                  className="page-input"
                />{' '}
                of {totalPages}
              </span>

              <button onClick={() => goToPage(currentPage + 1)} disabled={currentPage >= totalPages - 1}>
                Next
              </button>
              <button onClick={() => goToPage(totalPages - 1)} disabled={currentPage >= totalPages - 1}>
                Last
              </button>
            </div>
          </div>
        )}

        <table className="data-table">
          <thead>
            <tr>
              {visibleColumns.map((col) => (
                <th
                  key={col.key}
                  onClick={() => handleSort(col.key)}
                  className={col.sortable !== false ? 'sortable' : ''}
                  style={col.width ? { width: col.width } : {}}
                  title={col.tooltip || ''}
                >
                  <div className="th-content">
                    <span>{col.headerRender ? col.headerRender() : col.label}</span>
                    {col.sortable !== false && getSortIcon(col.key)}
                  </div>
                </th>
              ))}
            </tr>
            <tr className="filter-row">
              {visibleColumns.map((col) => (
                <th key={`filter-${col.key}`}>
                  {col.filterable !== false && !col.isAction && (
                    <input
                      type="text"
                      placeholder="Filter..."
                      value={filters[col.key] || ''}
                      onChange={(e) => handleFilterChange(col.key, e.target.value)}
                    />
                  )}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {paginatedData.length === 0 ? (
              <tr>
                <td colSpan={visibleColumns.length} className="no-data">
                  No data available
                </td>
              </tr>
            ) : (
              paginatedData.map((item, index) => (
                <tr
                  key={item.Id || index}
                  onClick={(event) => handleRowClick(event, item)}
                  className={onRowClick ? 'clickable' : ''}
                >
                  {visibleColumns.map((col) => (
                    <td key={col.key}>
                      {col.render ? col.render(item) : item[col.key]}
                    </td>
                  ))}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export default DataTable;
