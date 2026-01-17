import React, { useState, useMemo } from 'react';

function DataTable({
  data = [],
  columns = [],
  loading = false,
  pageSize: defaultPageSize = 25,
  onRowClick = null
}) {
  const [currentPage, setCurrentPage] = useState(0);
  const [pageSize, setPageSize] = useState(defaultPageSize);
  const [sortConfig, setSortConfig] = useState({ key: null, direction: 'asc' });
  const [filters, setFilters] = useState({});
  const [pageInput, setPageInput] = useState('1');

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

  if (loading) {
    return (
      <div className="data-table-loading">
        <div className="spinner"></div>
        <span>Loading...</span>
      </div>
    );
  }

  return (
    <div className="data-table-container">
      <table className="data-table">
        <thead>
          <tr>
            {columns.map((col) => (
              <th
                key={col.key}
                onClick={() => handleSort(col.key)}
                className={col.sortable !== false ? 'sortable' : ''}
                style={col.width ? { width: col.width } : {}}
              >
                <div className="th-content">
                  <span>{col.label}</span>
                  {col.sortable !== false && getSortIcon(col.key)}
                </div>
              </th>
            ))}
          </tr>
          <tr className="filter-row">
            {columns.map((col) => (
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
              <td colSpan={columns.length} className="no-data">
                No data available
              </td>
            </tr>
          ) : (
            paginatedData.map((item, index) => (
              <tr
                key={item.Id || index}
                onClick={() => onRowClick && onRowClick(item)}
                className={onRowClick ? 'clickable' : ''}
              >
                {columns.map((col) => (
                  <td key={col.key}>
                    {col.render ? col.render(item) : item[col.key]}
                  </td>
                ))}
              </tr>
            ))
          )}
        </tbody>
      </table>

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
    </div>
  );
}

export default DataTable;
