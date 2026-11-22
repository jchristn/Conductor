import React, { useState } from 'react';
import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import DecisionQuestionBuilder from '../src/components/DecisionQuestionBuilder';
import DecisionAnswersTable from '../src/components/DecisionAnswersTable';

function Harness({ initial }) {
  const [body, setBody] = useState(initial);
  return (
    <>
      <DecisionQuestionBuilder requestBody={body} onChange={setBody} />
      <pre data-testid="body">{body}</pre>
    </>
  );
}

const emptyBody = JSON.stringify({ model: 'jev-latest', state: '', questions: {} }, null, 2);

describe('Decision question builder', () => {
  it('adds binary, choice and score questions and generates System One JSON', () => {
    render(<Harness initial={emptyBody} />);
    fireEvent.change(screen.getByLabelText('State'), { target: { value: 'Refund my duplicate charge' } });
    fireEvent.click(screen.getByRole('button', { name: '+ Binary' }));
    fireEvent.click(screen.getByRole('button', { name: '+ Choice' }));
    fireEvent.click(screen.getByRole('button', { name: '+ Score' }));
    fireEvent.change(screen.getByLabelText('Question 1 instructions'), { target: { value: 'Is this a refund request?' } });
    fireEvent.change(screen.getByLabelText('Question 2 option 1 value'), { target: { value: 'refund' } });

    const body = JSON.parse(screen.getByTestId('body').textContent);
    expect(body.model).toBe('jev-latest');
    expect(body.state).toBe('Refund my duplicate charge');
    expect(body.questions.binary_1).toEqual({ type: 'noul', instructions: 'Is this a refund request?' });
    expect(body.questions.choice_1.type).toBe('choice');
    expect(Object.keys(body.questions.choice_1.criteria)).toContain('refund');
    expect(body.questions.score_1.type).toBe('score');
    expect(body.questions.score_1.criteria.length).toBeGreaterThan(1);

    fireEvent.click(screen.getByRole('button', { name: 'Remove question 2' }));
    expect(JSON.parse(screen.getByTestId('body').textContent).questions).not.toHaveProperty('choice_1');
  });

  it('asks for valid JSON instead of guessing when the body cannot be parsed', () => {
    render(<Harness initial="{oops" />);
    expect(screen.getByText(/not valid decision JSON/)).toBeTruthy();
  });

  it('renders answers with type, probability or score, and confidence', () => {
    render(<DecisionAnswersTable answers={[
      { id: 'q1', type: 'noul', instructions: 'Is it a question?', answer: 'Yes', value: 0.9, valueKind: 'probability', confidence: 0.75 },
      { id: 'q2', type: 'score', instructions: '', answer: 'high', value: 2.2, valueKind: 'score', confidence: null }
    ]} />);
    const table = screen.getByRole('table', { name: 'Decision answers' });
    expect(table.textContent).toMatch(/Binary \(noul\)/);
    expect(table.textContent).toMatch(/90%/);
    expect(table.textContent).toMatch(/75%/);
    expect(table.textContent).toMatch(/Score \(levels\)/);
    expect(table.textContent).toMatch(/2\.2/);
  });
});
