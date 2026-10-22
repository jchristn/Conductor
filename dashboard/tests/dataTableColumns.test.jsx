import React from 'react';
import { beforeEach, describe, it, expect } from 'vitest';
import { render, screen, fireEvent, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import DataTable from '../src/components/DataTable';

const columns = [
  { key: 'pick', label: '', sortable: false, filterable: false },
  { key: 'Id', label: 'ID', defaultHidden: true },
  { key: 'Name', label: 'Name' },
  { key: 'Created', label: 'Created' },
  { key: 'actions', label: 'Actions', isAction: true }
];
const data = [{ Id: 'id_1', Name: 'alpha', Created: 'today' }];

function renderTable(props = {}, path = '/things') {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <DataTable data={data} columns={columns} {...props} />
    </MemoryRouter>
  );
}

const headers = (container) => [...container.querySelectorAll('thead tr:first-child th')].map((th) => th.textContent.trim());

beforeEach(() => window.localStorage.clear());

describe('DataTable column selector', () => {
  it('lives in the pagination bar, not a separate toolbar', () => {
    const { container } = renderTable();
    const trigger = screen.getByRole('button', { name: /columns/i });
    expect(trigger.closest('.pagination-controls')).not.toBeNull();
    expect(container.querySelector('.data-table-toolbar')).toBeNull();
  });

  it('hides defaultHidden columns and leaves unlabeled utility columns out of the selector', () => {
    const { container } = renderTable();
    expect(headers(container)).toEqual(['', 'Name', 'Created', 'Actions']);
    fireEvent.click(screen.getByRole('button', { name: /columns/i }));
    const list = container.querySelector('.column-selector-list');
    expect(within(list).getAllByRole('checkbox').map((box) => box.parentElement.textContent)).toEqual(['ID', 'Name', 'Created']);
  });

  it('remembers choices per table and restores defaults', () => {
    const first = renderTable();
    fireEvent.click(screen.getByRole('button', { name: /columns/i }));
    fireEvent.click(screen.getByLabelText('ID'));
    fireEvent.click(screen.getByLabelText('Created'));
    expect(headers(first.container)).toEqual(['', 'ID', 'Name', 'Actions']);
    first.unmount();

    const second = renderTable();
    expect(headers(second.container)).toEqual(['', 'ID', 'Name', 'Actions']);
    fireEvent.click(screen.getByRole('button', { name: /columns/i }));
    fireEvent.click(screen.getByRole('button', { name: 'Defaults' }));
    expect(headers(second.container)).toEqual(['', 'Name', 'Created', 'Actions']);
    expect(window.localStorage.getItem('conductor_columns:/things')).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Show all' }));
    expect(headers(second.container)).toEqual(['', 'ID', 'Name', 'Created', 'Actions']);
  });

  it('ignores a stored choice that would hide every column', () => {
    window.localStorage.setItem('conductor_columns:/things', JSON.stringify(['Id', 'Name', 'Created']));
    const { container } = renderTable();
    expect(headers(container)).toEqual(['', 'Name', 'Created', 'Actions']);
  });

  it('renders caller-supplied paging controls next to the selector when pagination is external', () => {
    renderTable({ hidePagination: true, paginationInfo: 'Showing 1 to 1 of 99 entries', paginationControls: <button type="button">Next</button> });
    expect(screen.getByText('Showing 1 to 1 of 99 entries')).toBeTruthy();
    const controls = screen.getByRole('button', { name: 'Next' }).closest('.pagination-controls');
    expect(within(controls).getByRole('button', { name: /columns/i })).toBeTruthy();
  });

  it('keeps the pagination bar while loading', () => {
    const { container } = renderTable({ loading: true });
    expect(container.querySelector('.pagination')).not.toBeNull();
    expect(screen.getByText('Loading...')).toBeTruthy();
  });
});
